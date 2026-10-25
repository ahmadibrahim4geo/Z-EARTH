using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZEarth.App.Services;
using ZEarth.App.ViewModels;
using ZEarth.Domain;
using ZEarth.Domain.Crs;
using ZEarth.Globe;

namespace ZEarth.Tests
{
    /// <summary>
    /// Regression coverage for the UI-responsiveness remediation (RC-1..RC-9):
    /// non-blocking logging, the correlated bridge request protocol, targeted command updates,
    /// the save gate, and the globe DOM/JS contract.
    /// </summary>
    [TestClass]
    public class ResponsivenessRegressionTests
    {
        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "ZEarth.slnx")) ||
                    File.Exists(Path.Combine(dir.FullName, "ZEarth.sln")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            throw new InvalidOperationException("Repository root could not be located.");
        }

        private static string RepoFile(params string[] relativeParts)
        {
            var path = Path.Combine(new[] { FindRepoRoot() }.Concat(relativeParts).ToArray());
            Assert.IsTrue(File.Exists(path), $"Expected file not found: {path}");
            return path;
        }

        // ---------------------------------------------------------------------
        // RC-2: transform caching
        // ---------------------------------------------------------------------

        [TestMethod]
        public void CoordinateTransformService_TransformFast_MatchesTransform_AndIsCached()
        {
            const double lon = 31.1342;
            const double lat = 29.9792;

            var (x1, y1) = CoordinateTransformService.TransformFast(lon, lat, 4326, 3857);
            var (x2, y2) = CoordinateTransformService.TransformFast(lon, lat, 4326, 3857);

            Assert.AreEqual(x1, x2, 1e-9, "Cached transform must be deterministic");
            Assert.AreEqual(y1, y2, 1e-9);

            var (xRef, yRef) = CoordinateTransformService.Transform(lon, lat, 4326, 3857);
            // Either PROJ or the mathematical fallback may be used; both must agree closely.
            Assert.AreEqual(xRef, x1, Math.Max(1.0, Math.Abs(xRef) * 1e-6));
            Assert.AreEqual(yRef, y1, Math.Max(1.0, Math.Abs(yRef) * 1e-6));
        }

        // ---------------------------------------------------------------------
        // RC-3: correlated request protocol
        // ---------------------------------------------------------------------

        [TestMethod]
        public void GlobeBridge_AcceptedStatus_KeepsRequestAlive_ThenCompletedResolves()
        {
            var bridge = new GlobeBridge();
            string? sentJson = null;
            bridge.SendJsonToWebView = json =>
            {
                sentJson = json;
                return Task.CompletedTask;
            };

            var task = bridge.SendRequestAsync("camera.flyTo", null, 5000, "camera.view");
            Assert.IsNotNull(sentJson, "Request must be posted synchronously up to the first await");

            using var doc = JsonDocument.Parse(sentJson!);
            var reqId = doc.RootElement.GetProperty("requestId").GetString();
            Assert.IsFalse(string.IsNullOrEmpty(reqId));

            // "accepted" must NOT resolve the request.
            bridge.HandleWebMessage($"{{\"requestId\":\"{reqId}\",\"action\":\"camera.flyTo\",\"status\":\"accepted\"}}");
            Assert.IsFalse(task.IsCompleted, "An accepted acknowledgement must not resolve the waiter");

            bridge.HandleWebMessage($"{{\"requestId\":\"{reqId}\",\"action\":\"camera.flyTo\",\"status\":\"completed\",\"success\":true}}");
            Assert.IsTrue(task.Wait(2000), "Terminal status must resolve the waiter");
            Assert.IsTrue(task.Result);
        }

        [TestMethod]
        public async Task GlobeBridge_Timeout_RaisesRequestFailed_AndReturnsFalse()
        {
            var bridge = new GlobeBridge();
            bridge.SendJsonToWebView = _ => Task.CompletedTask;

            BridgeFailureInfo? failure = null;
            bridge.RequestFailed += (s, f) => failure = f;

            var ok = await bridge.SendRequestAsync("camera.home", null, 150);

            Assert.IsFalse(ok);
            Assert.IsNotNull(failure);
            Assert.IsTrue(failure!.TimedOut);
            Assert.AreEqual(0, bridge.PendingRequestCount, "Timed-out requests must be removed from the pending map");
        }

        [TestMethod]
        public async Task GlobeBridge_SupersedeGroup_CancelsOlderRequest()
        {
            var bridge = new GlobeBridge();
            bridge.SendJsonToWebView = _ => Task.CompletedTask;

            var first = bridge.SendRequestAsync("camera.flyTo", null, 5000, "camera.view");
            var second = bridge.SendRequestAsync("camera.flyTo", null, 5000, "camera.view");

            var firstResult = await first;
            Assert.IsFalse(firstResult, "The superseded request must complete as cancelled/failed");
            Assert.IsFalse(second.IsCompleted, "The newest request must remain in flight");
            Assert.AreEqual(1, bridge.PendingRequestCount);

            bridge.CancelAllPending("test cleanup");
            var secondResult = await second;
            Assert.IsFalse(secondResult);
        }

        [TestMethod]
        public void GlobeBridge_Counters_ClassifyInboundTraffic()
        {
            var bridge = new GlobeBridge();

            bridge.HandleWebMessage("{\"event\":\"camera.changed\",\"camera\":{\"latitude\":30,\"longitude\":31,\"altitude\":1000,\"heading\":0,\"pitch\":-90,\"roll\":0}}");
            bridge.HandleWebMessage("{\"event\":\"map.cursorMoved\",\"position\":{\"valid\":true,\"latitude\":30,\"longitude\":31,\"altitude\":0}}");
            bridge.HandleWebMessage("{\"event\":\"map.centerChanged\",\"center\":{\"isValid\":true,\"latitude\":30,\"longitude\":31,\"altitude\":0}}");
            bridge.HandleWebMessage("{\"event\":\"draw.completed\",\"geometryType\":\"point\"}");
            bridge.HandleWebMessage("{\"event\":\"js.error\",\"message\":\"boom\"}");
            bridge.HandleWebMessage("{\"command\":\"theme.toggle\"}");

            var c = bridge.SnapshotAndResetCounters();
            Assert.AreEqual(3, c.Telemetry, "camera.changed + cursorMoved + centerChanged are telemetry");
            Assert.AreEqual(1, c.Notable, "draw.completed is a notable lifecycle event");
            Assert.AreEqual(1, c.Errors, "js.error is an error event");
            Assert.AreEqual(1, c.Commands, "host commands are counted separately");

            // Reset must return to zero.
            var after = bridge.SnapshotAndResetCounters();
            Assert.AreEqual(0, after.Telemetry + after.Notable + after.Errors + after.Commands);
        }

        // ---------------------------------------------------------------------
        // RC-3: async command re-entrancy & error reporting
        // ---------------------------------------------------------------------

        [TestMethod]
        public void AsyncRelayCommand_PreventsReentrancy_AndReportsCompletion()
        {
            int executions = 0;
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var command = new AsyncRelayCommand(async () =>
            {
                Interlocked.Increment(ref executions);
                await gate.Task;
            });

            command.Execute(null);
            command.Execute(null); // must be ignored while the first is running

            Assert.AreEqual(1, executions, "Re-entrant execution must be suppressed");
            Assert.IsTrue(command.IsExecuting);

            gate.SetResult();
            Assert.IsTrue(SpinUntil(() => !command.IsExecuting, TimeSpan.FromSeconds(3)));
        }

        [TestMethod]
        public void AsyncRelayCommand_ReportsErrors_ToHandler()
        {
            Exception? reported = null;
            var command = new AsyncRelayCommand(
                () => throw new InvalidOperationException("boom"),
                null,
                ex => reported = ex);

            command.Execute(null);

            Assert.IsTrue(SpinUntil(() => reported != null, TimeSpan.FromSeconds(3)));
            Assert.IsInstanceOfType(reported, typeof(InvalidOperationException));
        }

        private static bool SpinUntil(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return true;
                Thread.Sleep(15);
            }
            return condition();
        }

        // ---------------------------------------------------------------------
        // RC-1: non-blocking logger
        // ---------------------------------------------------------------------

        [TestMethod]
        public void LoggerService_BurstEnqueue_DropsNothing_AndDrainsOnShutdown()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"zearth_log_{Guid.NewGuid():N}");
            var logger = new LoggerService(dir);

            for (int i = 0; i < 4000; i++)
            {
                logger.LogInfo($"burst-{i}");
            }
            logger.LogError("final-error", new InvalidOperationException("failure"));

            Assert.AreEqual(0, logger.DroppedEntryCount, "A burst below capacity must not drop entries");

            logger.Shutdown(TimeSpan.FromSeconds(10));

            var logPath = Path.Combine(dir, "zearth.log");
            Assert.IsTrue(File.Exists(logPath), "The isolated log file must have been written");

            try { Directory.Delete(dir, true); } catch { /* best effort */ }
        }

        // ---------------------------------------------------------------------
        // RC-7: single-writer save gate
        // ---------------------------------------------------------------------

        [TestMethod]
        public async Task ProjectService_ConcurrentSaves_AreSerialized_AndSucceed()
        {
            var service = new ProjectService();
            var target = Path.Combine(Path.GetTempPath(), $"zearth_gate_{Guid.NewGuid():N}.zearth");
            service.CurrentFilePath = target;

            try
            {
                var first = service.SaveAsync(target);
                var second = service.SaveAsync(target);
                var results = await Task.WhenAll(first, second);

                Assert.IsTrue(results.All(r => r), "Both serialized saves must succeed");
                Assert.IsTrue(File.Exists(target));
            }
            finally
            {
                if (File.Exists(target)) File.Delete(target);
            }
        }

        [TestMethod]
        public async Task ProjectService_SaveToLockedTarget_FailsGracefully_ThenSucceedsAfterUnlock()
        {
            var service = new ProjectService();
            var dir = Path.Combine(Path.GetTempPath(), $"zearth_lock_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            var target = Path.Combine(dir, "locked.zearth");

            // Make an initial valid bundle so the target exists.
            Assert.IsTrue(await service.SaveAsync(target));

            // Hold the target with an exclusive lock, simulating another process/app.
            using (var hold = new FileStream(target, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var savedWhileLocked = await service.SaveAsync(target);
                Assert.IsFalse(savedWhileLocked, "A save against a locked target must fail gracefully, not throw");
                hold.Dispose();
            }

            // No orphaned temporary archives may remain next to the target.
            Assert.AreEqual(0, Directory.GetFiles(dir, "*.tmp_*").Length, "Failed saves must clean up temp files");

            // Once the lock is released the same instance must be able to save again.
            Assert.IsTrue(await service.SaveAsync(target));
            Assert.IsTrue(File.Exists(target));

            try { Directory.Delete(dir, true); } catch { /* best effort */ }
        }

        // ---------------------------------------------------------------------
        // RC-8 / RC-9: globe DOM + JS contract + no developer paths
        // ---------------------------------------------------------------------

        [TestMethod]
        public void GlobeIndexHtml_DefinesEveryDomIdReferencedByEngine()
        {
            var js = File.ReadAllText(RepoFile("src", "ZEarth.Globe", "Assets", "cesium-engine.js"));
            var html = File.ReadAllText(RepoFile("src", "ZEarth.Globe", "Assets", "index.html"));

            var htmlIds = new HashSet<string>(
                Regex.Matches(html, "id=\"([^\"]+)\"").Select(m => m.Groups[1].Value),
                StringComparer.Ordinal);

            var jsIds = Regex.Matches(js, "getElementById\\(\\s*['\"]([^'\"]+)['\"]\\s*\\)")
                .Select(m => m.Groups[1].Value)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            // Regression gate requested by the audit (D2): the whole DOM contract, not just a sample.
            Assert.IsTrue(jsIds.Count >= 30, $"Expected many DOM id references in the engine, found {jsIds.Count}");

            var missing = jsIds.Where(id => !htmlIds.Contains(id)).ToList();
            Assert.AreEqual(0, missing.Count,
                "cesium-engine.js references DOM ids missing from index.html: " + string.Join(", ", missing));

            foreach (var id in new[] { "coord-display", "compass-ring", "attribution-pill" })
            {
                Assert.IsTrue(html.Contains($"id=\"{id}\""), $"index.html must define #{id}");
            }
        }

        [TestMethod]
        public void CesiumEngine_ImplementsAcceptedAndTerminalFlightProtocol()
        {
            var js = File.ReadAllText(RepoFile("src", "ZEarth.Globe", "Assets", "cesium-engine.js"));

            Assert.IsTrue(js.Contains("respondAccepted"), "JS must acknowledge requests with an accepted status");
            Assert.IsTrue(js.Contains("'cancelled'"), "JS must send terminal cancelled statuses");
            Assert.IsTrue(js.Contains("'failed'"), "JS must send terminal failed statuses");
            Assert.IsTrue(js.Contains("Unsupported action"), "JS must fail unknown actions immediately instead of timing out");
        }

        [TestMethod]
        public void GlobeBridge_EveryRequestActionHasAJavaScriptHandler()
        {
            var bridge = File.ReadAllText(RepoFile("src", "ZEarth.Globe", "GlobeBridge.cs"));
            var engine = File.ReadAllText(RepoFile("src", "ZEarth.Globe", "Assets", "cesium-engine.js"));

            var sentActions = Regex.Matches(bridge, "SendRequestAsync\\(\\\"([^\\\"]+)\\\"")
                .Select(match => match.Groups[1].Value)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var handledActions = new HashSet<string>(
                Regex.Matches(engine, "case\\s+'([^']+)'\\s*:")
                    .Select(match => match.Groups[1].Value),
                StringComparer.Ordinal);

            var missing = sentActions.Where(action => !handledActions.Contains(action)).ToList();
            Assert.AreEqual(0, missing.Count,
                "Host actions without Cesium handlers: " + string.Join(", ", missing));
        }

        [TestMethod]
        public async Task GlobeBridge_Timeout_SendsCancellationNotificationToEngine()
        {
            var bridge = new GlobeBridge();
            var sent = new List<string>();
            bridge.SendJsonToWebView = json => { sent.Add(json); return Task.CompletedTask; };

            var ok = await bridge.SendRequestAsync("camera.flyTo", null, 150);

            Assert.IsFalse(ok);
            Assert.IsTrue(
                SpinUntil(() => sent.Any(m => m.Contains("request.cancel")), TimeSpan.FromSeconds(3)),
                "A timed-out request must notify the engine so orphaned work (e.g. an interrupted flight) is aborted");
        }

        [TestMethod]
        public void CesiumEngine_ImplementsHostCancellationContract()
        {
            var js = File.ReadAllText(RepoFile("src", "ZEarth.Globe", "Assets", "cesium-engine.js"));

            Assert.IsTrue(js.Contains("'request.cancel'"), "JS must handle host cancellation notifications (RC-3)");
            Assert.IsTrue(js.Contains("cancelFlight"), "JS must abort the in-flight camera animation on cancellation");
        }

        [TestMethod]
        public void CesiumEngine_UsesGrabOrbitAndPointerZoomNavigationContract()
        {
            var js = File.ReadAllText(RepoFile("src", "ZEarth.Globe", "Assets", "cesium-engine.js"));

            Assert.IsTrue(js.Contains("controller.rotateEventTypes = [Cesium.CameraEventType.LEFT_DRAG]"),
                "Left drag must rotate/grab the 3D globe");
            Assert.IsTrue(js.Contains("controller.translateEventTypes = [Cesium.CameraEventType.LEFT_DRAG]"),
                "Left drag must translate the 2D map");
            Assert.IsTrue(js.Contains("controller.zoomEventTypes = [Cesium.CameraEventType.WHEEL, Cesium.CameraEventType.PINCH]"),
                "Wheel and pinch must be the only continuous zoom gestures");
            Assert.IsTrue(js.Contains("Cesium.CameraEventType.RIGHT_DRAG"),
                "Right drag must be available for orbit/tilt");
            Assert.IsTrue(js.Contains("function zoomAtScreenPosition"),
                "Button and double-click zoom must use the cursor anchor");
            Assert.IsTrue(js.Contains("camera.position.x += beforeProjected.x - afterProjected.x") &&
                          js.Contains("camera.position.y += beforeProjected.y - afterProjected.y"),
                "2D zoom must preserve the ground coordinate under the cursor");
        }

        [TestMethod]
        public void Compass_StaysCenteredAndQiblaBearingTracksCameraHeading()
        {
            var js = File.ReadAllText(RepoFile("src", "ZEarth.Globe", "Assets", "cesium-engine.js"));
            var css = File.ReadAllText(RepoFile("src", "ZEarth.Globe", "Assets", "globe.css"));

            Assert.IsTrue(js.Contains("mode === 'cardinal' ? 'flex' : 'none'") &&
                          js.Contains("mode === 'heading' ? 'flex' : 'none'") &&
                          js.Contains("mode === 'qibla' ? 'flex' : 'none'"),
                "Every compass face must retain flex centering when activated");
            Assert.IsTrue(js.Contains("qiblaAzimuth - heading"),
                "The geographic Qibla bearing must be transformed into screen space");
            Assert.IsTrue(js.Contains("requestAnimationFrame"),
                "Compass repaint must be coalesced to animation frames during camera movement");
            Assert.IsTrue(css.Contains("transform: translate(-50%, -50%) rotate(var(--qibla-angle, 0deg))"),
                "Qibla needle rotation must preserve its centered translation");
        }

        [TestMethod]
        public void ViewModel_ContainsNoBlockingDispatcherInvoke()
        {
            var source = File.ReadAllText(RepoFile("src", "ZEarth.App", "ViewModels", "MainViewModel.cs"));
            Assert.IsFalse(source.Contains("Dispatcher.Invoke("),
                "The view model must never block a thread on the UI dispatcher (RC-4) - use RunOnUi/BeginInvoke");
        }

        [TestMethod]
        public void GlobeBridge_ContainsNoHardcodedDeveloperPaths()
        {
            var source = File.ReadAllText(RepoFile("src", "ZEarth.Globe", "GlobeBridge.cs"));
            Assert.IsFalse(source.Contains("D:\\My Software"), "Developer-machine asset paths must not be compiled in (RC-9)");
        }

        [TestMethod]
        public void ThemeService_UsesComponentResourceUris()
        {
            var source = File.ReadAllText(RepoFile("src", "ZEarth.App", "Services", "ThemeService.cs"));
            Assert.IsTrue(source.Contains("/ZEarth;component/Themes/DarkTheme.xaml"), "Theme URIs must resolve to this assembly (RC-6)");
            Assert.IsFalse(source.Contains("MergedDictionaries.Clear()"), "Theme switching must not clear all merged resources (RC-6)");
        }
    }
}
