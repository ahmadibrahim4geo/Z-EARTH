using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ZEarth.Domain;

namespace ZEarth.Globe
{
    public sealed record TerrainHeightSample(double Longitude, double Latitude, double ElevationMeters);

    public sealed class GlobeBridge : IMapViewport
    {
        /// <summary>
        /// A correlated host→JS request. Tracks its own deadline so an in-flight request can be
        /// acknowledged with an <c>accepted</c> status and later resolved by a terminal status
        /// (completed / cancelled / failed) without a silent 3 second failure (RC-3).
        /// </summary>
        private sealed class PendingRequest
        {
            public string RequestId { get; init; } = string.Empty;
            public string Action { get; init; } = string.Empty;
            public string Group { get; init; } = string.Empty;
            public TaskCompletionSource<BridgeResponse> Completion { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            public long DeadlineUtcTicks;

            private int _completed;

            public void ExtendDeadline(int milliseconds)
            {
                var extended = DateTime.UtcNow.AddMilliseconds(milliseconds).Ticks;
                long current;
                do
                {
                    current = Interlocked.Read(ref DeadlineUtcTicks);
                    if (extended <= current) return;
                }
                while (Interlocked.CompareExchange(ref DeadlineUtcTicks, extended, current) != current);
            }

            public bool TryResolve(BridgeResponse response)
            {
                if (Interlocked.Exchange(ref _completed, 1) != 0) return false;
                return Completion.TrySetResult(response);
            }

            public bool TryCancel(string reason)
                => TryResolve(new BridgeResponse
                {
                    RequestId = RequestId,
                    Action = Action,
                    Status = BridgeStatus.Cancelled,
                    Success = false,
                    Error = reason
                });
        }

        private readonly ConcurrentDictionary<string, PendingRequest> _pendingRequests = new();

        /// <summary>Default request timeout when no operation-specific value is supplied.</summary>
        public const int DefaultTimeoutMs = 8000;
        private const int DefaultAcceptExtensionMs = 8000;
        private const string CameraViewGroup = "camera.view";

        private long _telemetryEvents;
        private long _notableEvents;
        private long _commandEvents;
        private long _errorEvents;

        public CameraState CurrentCamera { get; private set; } = CameraState.CreateHome();
        public SceneMode CurrentMode { get; private set; } = SceneMode.Scene3D;
        public MapCenterInfo CurrentCenter { get; private set; } = new MapCenterInfo { IsValid = true, Latitude = 26.8206, Longitude = 30.8025, Altitude = 0.0 };

        public event EventHandler<CameraState>? CameraChanged;
        public event EventHandler<CameraState>? CameraMoveEnded;
        public event EventHandler<MapCenterInfo>? MapCenterChanged;
        public event EventHandler<CursorPositionInfo>? CursorPositionChanged;
        public event EventHandler<SceneMode>? SceneModeChanged;
        public event EventHandler<string>? EngineReady;
        public event EventHandler<string>? WebError;
        public event EventHandler<string>? CommandRequested;
        public event EventHandler<string>? DrawCompleted;
        public event EventHandler<string>? FeaturePicked;
        public event EventHandler<string>? MeasurementUpdated;
        public event EventHandler<string>? MeasurementCompleted;
        public event EventHandler<string>? LayerAttributionChanged;
        public event EventHandler<string>? BasemapChanged;
        public event EventHandler<string>? TerrainFallbackOccurred;
        public event EventHandler<string>? TerrainChanged;
        public event EventHandler<double>? TimeOfDayChanged;
        // Carries UTC date and whether the globe's Now button was pressed.
        public event EventHandler<string>? TimeContextChanged;
        public event EventHandler<bool>? SunlightChanged;
        public event EventHandler<bool>? AtmosphereChanged;
        public event EventHandler<bool>? CloudsChanged;
        public event EventHandler<bool>? FlightModeChanged;
        public event EventHandler<bool>? GpsPlaybackChanged;
        public event EventHandler<string>? DeviceLocationReceived;

        /// <summary>Raised when a correlated request fails, is cancelled or times out (RC-3).</summary>
        public event EventHandler<BridgeFailureInfo>? RequestFailed;

        public Func<string, Task>? SendJsonToWebView { get; set; }

        public int PendingRequestCount => _pendingRequests.Count;

        public (long Telemetry, long Notable, long Commands, long Errors, int Pending) SnapshotAndResetCounters()
        {
            var snapshot = (
                Telemetry: Interlocked.Exchange(ref _telemetryEvents, 0),
                Notable: Interlocked.Exchange(ref _notableEvents, 0),
                Commands: Interlocked.Exchange(ref _commandEvents, 0),
                Errors: Interlocked.Exchange(ref _errorEvents, 0),
                Pending: _pendingRequests.Count);
            return snapshot;
        }

        private static readonly HashSet<string> TelemetryEventNames = new(StringComparer.Ordinal)
        {
            "camera.changed",
            "camera.moveEnd",
            "map.centerChanged",
            "map.cursorMoved"
        };

        public static string GetAssetsDirectory()
        {
            // Assets are published next to the executable (CopyToOutputDirectory). Only the deployed
            // locations are considered — no developer-machine paths (RC-9).
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var directCandidates = new[]
            {
                Path.Combine(baseDir, "GlobeAssets"),
                Path.Combine(baseDir, "Assets")
            };

            foreach (var candidate in directCandidates)
            {
                if (File.Exists(Path.Combine(candidate, "index.html")))
                {
                    return candidate;
                }
            }

            // 2. Otherwise extract embedded resources to LocalApplicationData/ZEarth/GlobeAssets
            var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZEarth", "GlobeAssets");
            Directory.CreateDirectory(appData);

            var assembly = typeof(GlobeBridge).Assembly;
            var resourceNames = assembly.GetManifestResourceNames();

            foreach (var resName in resourceNames)
            {
                if (!resName.Contains("Assets.")) continue;

                var fileName = resName.Substring(resName.IndexOf("Assets.") + "Assets.".Length);
                var destPath = Path.Combine(appData, fileName);

                try
                {
                    using var stream = assembly.GetManifestResourceStream(resName);
                    if (stream != null)
                    {
                        using var fileStream = File.Create(destPath);
                        stream.CopyTo(fileStream);
                    }
                }
                catch
                {
                    // Ignore extraction overwrite errors if already in use
                }
            }

            return appData;
        }

        /// <summary>
        /// Validates that the globe assets required at runtime are present and readable. Used at
        /// startup so a missing asset produces a clear diagnostic instead of a blank viewport and
        /// silent request timeouts (RC-8/RC-9).
        /// </summary>
        public static bool TryGetValidatedAssetsDirectory(out string assetsDir, out string error)
        {
            assetsDir = string.Empty;
            error = string.Empty;
            try
            {
                assetsDir = GetAssetsDirectory();
                var required = new[]
                {
                    "index.html",
                    "cesium-engine.js",
                "rectangle-geometry.js",
                "historical-timeline.js",
                    Path.Combine("cesium", "Cesium.js")
                };

                foreach (var relative in required)
                {
                    var full = Path.Combine(assetsDir, relative);
                    if (!File.Exists(full))
                    {
                        error = $"Required globe asset is missing: {full}";
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                error = $"Failed to resolve globe assets: {ex.Message}";
                return false;
            }
        }

        public void HandleWebMessage(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.String)
                {
                    var innerJson = root.GetString();
                    if (!string.IsNullOrEmpty(innerJson))
                    {
                        using var innerDoc = JsonDocument.Parse(innerJson);
                        ProcessMessageRoot(innerDoc.RootElement, innerJson);
                        return;
                    }
                }
                ProcessMessageRoot(root, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GlobeBridge] Error handling message: {ex.Message}");
            }
        }

        private void ProcessMessageRoot(JsonElement root, string json)
        {
            var hasRequestId = root.TryGetProperty("requestId", out var reqProp)
                               && reqProp.ValueKind == JsonValueKind.String
                               && !string.IsNullOrEmpty(reqProp.GetString());

            // Classify inbound traffic for cheap aggregate telemetry (RC-1). Streaming camera/cursor
            // events are counted separately from lifecycle and command messages. Correlated responses
            // are excluded even though they also carry an "action" property.
            if (!hasRequestId && (root.TryGetProperty("command", out _) || root.TryGetProperty("action", out _)))
            {
                Interlocked.Increment(ref _commandEvents);
            }
            else if (!hasRequestId && root.TryGetProperty("event", out var classifyProp) && classifyProp.ValueKind == JsonValueKind.String)
            {
                var name = classifyProp.GetString() ?? string.Empty;
                if (name == "js.error" || name == "js.unhandledrejection" || name == "imagery.error")
                {
                    Interlocked.Increment(ref _errorEvents);
                }
                else if (TelemetryEventNames.Contains(name))
                {
                    Interlocked.Increment(ref _telemetryEvents);
                }
                else
                {
                    Interlocked.Increment(ref _notableEvents);
                }
            }

            // Correlated responses are dispatched FIRST: a response also carries an "action" field,
            // so it must not be mistaken for a host command (RC-3).
            if (hasRequestId)
            {
                HandleCorrelatedResponse(reqProp.GetString()!, json);
                return;
            }

            // Check for host command requests from JS/bridge
            if (root.TryGetProperty("command", out var cmdProp) || root.TryGetProperty("action", out cmdProp))
            {
                var cmd = cmdProp.GetString();
                if (!string.IsNullOrEmpty(cmd))
                {
                    CommandRequested?.Invoke(this, cmd);
                    return;
                }
            }

            // Check for notifications
            if (root.TryGetProperty("event", out var evProp))
            {
                var ev = evProp.GetString();
                if (ev == "device.locationFix")
                {
                    DeviceLocationReceived?.Invoke(this, json);
                    return;
                }
                if (ev == "engine.ready")
                {
                    EngineReady?.Invoke(this, json);
                    return;
                }
                if (ev == "js.error" || ev == "js.unhandledrejection" || ev == "imagery.error")
                {
                    WebError?.Invoke(this, json);
                    return;
                }
                if (ev == "map.centerChanged")
                {
                    if (root.TryGetProperty("center", out var centerProp))
                    {
                        var center = JsonSerializer.Deserialize<MapCenterInfo>(centerProp.GetRawText());
                        if (center != null)
                        {
                            CurrentCenter = center;
                            MapCenterChanged?.Invoke(this, center);
                        }
                    }
                    return;
                }
                if (ev == "map.cursorMoved")
                {
                    if (root.TryGetProperty("position", out var posProp))
                    {
                        var pos = JsonSerializer.Deserialize<CursorPositionInfo>(posProp.GetRawText());
                        if (pos != null)
                        {
                            CursorPositionChanged?.Invoke(this, pos);
                        }
                    }
                    return;
                }
                if (ev == "camera.changed")
                {
                    if (root.TryGetProperty("camera", out var camProp))
                    {
                        var cam = JsonSerializer.Deserialize<CameraState>(camProp.GetRawText());
                        if (cam != null)
                        {
                            cam.Sanitize();
                            CurrentCamera = cam;
                            CameraChanged?.Invoke(this, cam);
                        }
                    }
                    return;
                }
                if (ev == "camera.moveEnd")
                {
                    if (root.TryGetProperty("camera", out var camProp))
                    {
                        var cam = JsonSerializer.Deserialize<CameraState>(camProp.GetRawText());
                        if (cam != null)
                        {
                            cam.Sanitize();
                            CurrentCamera = cam;
                            CameraMoveEnded?.Invoke(this, cam);
                        }
                    }
                    return;
                }
                if (ev == "scene.modeChanged")
                {
                    if (root.TryGetProperty("mode", out var modeProp))
                    {
                        var modeStr = modeProp.GetString();
                        if (Enum.TryParse<SceneMode>(modeStr, out var newMode))
                        {
                            CurrentMode = newMode;
                            SceneModeChanged?.Invoke(this, newMode);
                        }
                    }
                    return;
                }
                if (ev == "draw.completed")
                {
                    DrawCompleted?.Invoke(this, json);
                    return;
                }
                if (ev == "feature.picked")
                {
                    FeaturePicked?.Invoke(this, json);
                    return;
                }
                if (ev == "measurement.updated")
                {
                    MeasurementUpdated?.Invoke(this, json);
                    return;
                }
                if (ev == "measurement.completed")
                {
                    MeasurementCompleted?.Invoke(this, json);
                    return;
                }
                if (ev == "imagery.attribution")
                {
                    if (root.TryGetProperty("attribution", out var aProp))
                    {
                        LayerAttributionChanged?.Invoke(this, aProp.GetString() ?? "");
                    }
                    return;
                }
                if (ev == "imagery.basemapChanged")
                {
                    if (root.TryGetProperty("basemap", out var bProp))
                    {
                        BasemapChanged?.Invoke(this, bProp.GetString() ?? "");
                    }
                    return;
                }
                if (ev == "terrain.fallback")
                {
                    TerrainFallbackOccurred?.Invoke(this, json);
                    return;
                }
                if (ev == "terrain.changed")
                {
                    TerrainChanged?.Invoke(this, json);
                    return;
                }
                if (ev == "environment.timeChanged")
                {
                    if (root.TryGetProperty("timeHours", out var tProp) && tProp.TryGetDouble(out var th))
                    {
                        TimeOfDayChanged?.Invoke(this, th);
                        TimeContextChanged?.Invoke(this, json);
                    }
                    return;
                }
                if (ev == "environment.sunlightChanged")
                {
                    if (root.TryGetProperty("enabled", out var sProp))
                    {
                        SunlightChanged?.Invoke(this, sProp.GetBoolean());
                    }
                    return;
                }
                if (ev == "environment.atmosphereChanged")
                {
                    if (root.TryGetProperty("enabled", out var aProp))
                    {
                        AtmosphereChanged?.Invoke(this, aProp.GetBoolean());
                    }
                    return;
                }
                if (ev == "environment.cloudsChanged")
                {
                    if (root.TryGetProperty("enabled", out var cProp) &&
                        cProp.ValueKind is JsonValueKind.True or JsonValueKind.False)
                        CloudsChanged?.Invoke(this, cProp.GetBoolean());
                    return;
                }
                if (ev == "flight.changed")
                {
                    if (root.TryGetProperty("enabled", out var activeProp) &&
                        activeProp.ValueKind is JsonValueKind.True or JsonValueKind.False)
                        FlightModeChanged?.Invoke(this, activeProp.GetBoolean());
                    return;
                }
                if (ev == "gps.playbackChanged")
                {
                    if (root.TryGetProperty("enabled", out var playbackProp) &&
                        playbackProp.ValueKind is JsonValueKind.True or JsonValueKind.False)
                        GpsPlaybackChanged?.Invoke(this, playbackProp.GetBoolean());
                    return;
                }
            }

            // (Correlated responses are handled at the top of this method.)
        }

        private void HandleCorrelatedResponse(string reqId, string json)
        {
            if (!_pendingRequests.TryGetValue(reqId, out var pending)) return;

            var response = JsonSerializer.Deserialize<BridgeResponse>(json)
                           ?? new BridgeResponse { Success = false, Error = "Deserialization failed" };

            // An "accepted" acknowledgement keeps the request alive and extends its deadline
            // instead of resolving it (RC-3).
            if (string.Equals(response.Status, BridgeStatus.Accepted, StringComparison.OrdinalIgnoreCase))
            {
                pending.ExtendDeadline(DefaultAcceptExtensionMs);
                return;
            }

            if (_pendingRequests.TryRemove(reqId, out var removed))
            {
                removed.TryResolve(response);
            }
        }

        /// <summary>
        /// Sends a correlated request to the JS engine and awaits a terminal response. Requests may be
        /// acknowledged with an <c>accepted</c> status, which extends the deadline; on timeout the
        /// pending entry is removed, a warning is logged and <see cref="RequestFailed"/> is raised so
        /// the UI can surface the failure instead of silently ignoring it (RC-3).
        /// </summary>
        /// <param name="supersedeGroup">
        /// Optional logical group; a newer request in the same group cancels the older in-flight
        /// request (used so rapid camera flights do not pile up).
        /// </param>
        public async Task<bool> SendRequestAsync(
            string action,
            object? payload = null,
            int timeoutMs = DefaultTimeoutMs,
            string? supersedeGroup = null,
            CancellationToken cancellationToken = default)
        {
            var response = await SendRequestWithResponseAsync(action, payload, timeoutMs, supersedeGroup, cancellationToken);
            return response?.Success == true;
        }

        private async Task<BridgeResponse?> SendRequestWithResponseAsync(
            string action,
            object? payload = null,
            int timeoutMs = DefaultTimeoutMs,
            string? supersedeGroup = null,
            CancellationToken cancellationToken = default)
        {
            if (SendJsonToWebView == null) return null;

            if (!string.IsNullOrEmpty(supersedeGroup))
            {
                SupersedeGroup(supersedeGroup);
            }

            var request = new BridgeRequest
            {
                Action = action,
                Payload = payload
            };

            var pending = new PendingRequest
            {
                RequestId = request.RequestId,
                Action = action,
                Group = supersedeGroup ?? string.Empty
            };
            pending.ExtendDeadline(timeoutMs);
            _pendingRequests[request.RequestId] = pending;

            var json = JsonSerializer.Serialize(request);
            try
            {
                await SendJsonToWebView(json);
            }
            catch (Exception ex)
            {
                _pendingRequests.TryRemove(request.RequestId, out _);
                pending.TryCancel("Failed to send request to the globe engine.");
                RaiseFailure(request.RequestId, action, ex.Message, timedOut: false);
                return null;
            }

            CancellationTokenRegistration registration = default;
            if (cancellationToken.CanBeCanceled)
            {
                registration = cancellationToken.Register(() =>
                {
                    if (_pendingRequests.TryRemove(request.RequestId, out var p))
                    {
                        p.TryCancel("Cancelled by caller.");
                    }
                });
            }

            bool timedOut = false;
            try
            {
                while (!pending.Completion.Task.IsCompleted)
                {
                    var remainingTicks = Interlocked.Read(ref pending.DeadlineUtcTicks) - DateTime.UtcNow.Ticks;
                    if (remainingTicks <= 0)
                    {
                        timedOut = true;
                        break;
                    }

                    var winner = await Task.WhenAny(pending.Completion.Task, Task.Delay(TimeSpan.FromTicks(remainingTicks), cancellationToken));
                    if (winner == pending.Completion.Task) break;

                    cancellationToken.ThrowIfCancellationRequested();
                    // The delay elapsed; loop again in case an "accepted" acknowledgement extended
                    // the deadline in the meantime.
                }
            }
            catch (OperationCanceledException)
            {
                pending.TryCancel("Cancelled by caller.");
            }
            finally
            {
                registration.Dispose();
                _pendingRequests.TryRemove(request.RequestId, out _);
            }

            if (timedOut && !pending.Completion.Task.IsCompleted)
            {
                pending.TryCancel($"Timed out after {timeoutMs} ms.");
                RaiseFailure(request.RequestId, action, $"Request timed out after {timeoutMs} ms.", timedOut: true);
                // Abort the orphaned work in the engine so an abandoned command (e.g. an interrupted
                // camera flight) cannot keep animating/streaming on the host's behalf (RC-3).
                _ = NotifyAsync("request.cancel", new { action, requestId = request.RequestId });
                return null;
            }

            if (!pending.Completion.Task.IsCompleted)
            {
                return null;
            }

            var response = await pending.Completion.Task;
            if (!response.Success)
            {
                // Caller-initiated and superseded cancellations are expected and are not reported as
                // failures; genuine failures (e.g. an unsupported action) are surfaced.
                if (!string.Equals(response.Status, BridgeStatus.Cancelled, StringComparison.OrdinalIgnoreCase))
                {
                    RaiseFailure(request.RequestId, action, response.Error ?? "Request failed.", timedOut: false);
                }
            }
            return response;
        }

        /// <summary>
        /// Samples the currently activated terrain provider, not stored path vertex Z.
        /// Missing/ellipsoid/failed coverage yields null, never fabricated altitudes.
        /// Limited to 256 geographic points; JS does the actual sampling.
        /// </summary>
        public async Task<IReadOnlyList<TerrainHeightSample>?> SampleTerrainHeightsAsync(
            IReadOnlyList<CoordinatePoint> coordinates,
            CancellationToken cancellationToken = default)
        {
            if (coordinates == null || coordinates.Count < 2 || coordinates.Count > 256)
                return null;
            if (coordinates.Any(p => !double.IsFinite(p.Latitude) || !double.IsFinite(p.Longitude)
                || p.Latitude < -90 || p.Latitude > 90 || p.Longitude < -180 || p.Longitude > 180))
                return null;

            var response = await SendRequestWithResponseAsync("terrain.samplePath",
                new { points = coordinates.Select(p => new { longitude = p.Longitude, latitude = p.Latitude }).ToArray() },
                timeoutMs: 45000, cancellationToken: cancellationToken);
            if (response?.Success != true || response.Payload is not JsonElement json ||
                !json.TryGetProperty("samples", out var values) || values.ValueKind != JsonValueKind.Array ||
                values.GetArrayLength() != coordinates.Count)
                return null;
            var result = new List<TerrainHeightSample>(coordinates.Count);
            foreach (var value in values.EnumerateArray())
            {
                if (!value.TryGetProperty("longitude", out var longitude) ||
                    !value.TryGetProperty("latitude", out var latitude) ||
                    !value.TryGetProperty("height", out var height) ||
                    longitude.ValueKind != JsonValueKind.Number ||
                    latitude.ValueKind != JsonValueKind.Number ||
                    height.ValueKind != JsonValueKind.Number)
                    return null;
                var lon = longitude.GetDouble();
                var lat = latitude.GetDouble();
                var elev = height.GetDouble();
                if (!double.IsFinite(lon) || !double.IsFinite(lat) || !double.IsFinite(elev))
                    return null;
                result.Add(new TerrainHeightSample(lon, lat, elev));
            }
            return result;
        }


        private void SupersedeGroup(string group)
        {
            foreach (var kvp in _pendingRequests)
            {
                if (string.Equals(kvp.Value.Group, group, StringComparison.Ordinal))
                {
                    if (_pendingRequests.TryRemove(kvp.Key, out var outdated))
                    {
                        outdated.TryCancel("Superseded by a newer request.");
                    }
                }
            }
        }

        /// <summary>
        /// Fire-and-forget notification to the globe engine. Unlike <see cref="SendRequestAsync"/> it is
        /// not correlated and never times out, so it is safe to use for aborting work the host has
        /// already abandoned (RC-3) without creating a new orphaned pending request.
        /// </summary>
        public async Task NotifyAsync(string action, object? payload = null)
        {
            if (SendJsonToWebView == null) return;
            try
            {
                var notification = new BridgeRequest { Action = action, Payload = payload };
                await SendJsonToWebView(JsonSerializer.Serialize(notification));
            }
            catch
            {
                // Best effort only: notifications must never surface as user-visible failures.
            }
        }

        /// <summary>Cancels every in-flight request (used during shutdown / engine reload).</summary>
        public void CancelAllPending(string reason = "The globe engine was reloaded.")
        {
            foreach (var kvp in _pendingRequests)
            {
                if (_pendingRequests.TryRemove(kvp.Key, out var pending))
                {
                    pending.TryCancel(reason);
                }
            }
        }

        private void RaiseFailure(string requestId, string action, string reason, bool timedOut)
        {
            try
            {
                RequestFailed?.Invoke(this, new BridgeFailureInfo
                {
                    RequestId = requestId,
                    Action = action,
                    Reason = reason,
                    TimedOut = timedOut
                });
            }
            catch
            {
                // Reporting a failure must never throw
            }
        }

        /// <summary>Explicit interface implementation (kept in sync with IMapViewport).</summary>
        Task<bool> IMapViewport.FlyToAsync(CameraState target, double durationSeconds)
            => FlyToAsync(target, durationSeconds, CancellationToken.None);

        public async Task<bool> FlyToAsync(CameraState target, double durationSeconds = 1.5, CancellationToken cancellationToken = default)
        {            target.Sanitize();
            var timeoutMs = (int)(durationSeconds * 1000) + 6000;
            return await SendRequestAsync("camera.flyTo", new FlyToPayload
            {
                Latitude = target.Latitude,
                Longitude = target.Longitude,
                Altitude = target.Altitude,
                Heading = target.Heading,
                Pitch = target.Pitch,
                DurationSeconds = durationSeconds
            }, timeoutMs, CameraViewGroup, cancellationToken);
        }

        public async Task<bool> SetSceneModeAsync(SceneMode mode)
        {
            CurrentMode = mode;
            var success = await SendRequestAsync("scene.setMode", new SetSceneModePayload
            {
                Mode = mode.ToString()
            });

            if (success)
            {
                SceneModeChanged?.Invoke(this, mode);
            }
            return success;
        }

        public Task<bool> ZoomInAsync(double factor = 1.5) => SendRequestAsync("camera.zoomIn");
        public Task<bool> ZoomOutAsync(double factor = 1.5) => SendRequestAsync("camera.zoomOut");
        public Task<bool> SetZoomFactorAsync(double factor) => SendRequestAsync("camera.setZoomFactor", new { factor });
        public Task<bool> FitSelectionAsync(object payload) => SendRequestAsync("camera.fitSelection", payload, 12000, CameraViewGroup);
        public Task<bool> ResetNorthAsync() => SendRequestAsync("camera.resetNorth", null, DefaultTimeoutMs, CameraViewGroup);
        public Task<bool> ResetPitchAsync() => SendRequestAsync("camera.resetPitch", null, DefaultTimeoutMs, CameraViewGroup);
        public Task<bool> HomeAsync() => SendRequestAsync("camera.home", null, 12000, CameraViewGroup);
        public Task<bool> HomePresetAsync(HomePreset preset)
        {
            var target = CameraState.CreateHome(preset);
            return FlyToAsync(target);
        }
        public Task<bool> FullExtentAsync() => SendRequestAsync("camera.fullExtent", null, 12000, CameraViewGroup);
        public Task<bool> SetThemeAsync(string theme) => SendRequestAsync("app.setTheme", new { theme });
        // WebView2 must show an explicit permission prompt before acquiring a fix.
        public Task<bool> RequestDeviceLocationAsync() => SendRequestAsync("device.requestLocation", null, timeoutMs: 22000);

        public Task<bool> SetPlacemarksAsync(object placemarks) => SendRequestAsync("placemarks.set", placemarks);

        public Task<bool> StartDrawingAsync(string type, object? style = null) => SendRequestAsync("draw.start", new { type, style });
        public Task<bool> FinishDrawingAsync() => SendRequestAsync("draw.finish");
        public Task<bool> CancelDrawingAsync() => SendRequestAsync("draw.cancel");

        public Task<bool> StartMeasurementAsync(string type, object? options = null) => SendRequestAsync("measure.start", new { type, options });
        public Task<bool> FinishMeasurementAsync() => SendRequestAsync("measure.finish");
        public Task<bool> ClearMeasurementAsync() => SendRequestAsync("measure.clear");

        public Task<bool> SetSnapConfigAsync(bool enabled, double tolerancePixels = 12.0, bool snapVertices = true, bool snapMidpoints = true, bool snapEdges = true)
            => SendRequestAsync("snap.setConfig", new { enabled, tolerancePixels, snapVertices, snapMidpoints, snapEdges });

        public Task<bool> SetSnappingConfigAsync(bool enabled, string mode, double tolerancePixels, double groundToleranceMeters, bool snapVertices, bool snapMidpoints, bool snapEdges, bool snapIntersections)
            => SendRequestAsync("snap.setConfig", new { enabled, mode, tolerancePixels, groundToleranceMeters, snapVertices, snapMidpoints, snapEdges, snapIntersections });

        public Task<bool> SetUnitsConfigAsync(string distanceUnit, string areaUnit, string elevationUnit)
            => SendRequestAsync("units.set", new { distanceUnit, areaUnit, elevationUnit });

        public Task<bool> AddRasterOverlayAsync(string id, string name, string imageUrl, double west, double south, double east, double north, double opacity = 1.0)
            => SendRequestAsync("layers.addRasterOverlay", new { id, name, imageUrl, west, south, east, north, opacity });

        public Task<bool> RemoveRasterOverlayAsync(string id)
            => SendRequestAsync("layers.removeRasterOverlay", new { id });

        public Task<bool> SetTerrainExaggerationAsync(double factor) => SendRequestAsync("terrain.setExaggeration", new { factor });
        public Task<bool> ResetTerrainExaggerationAsync() => SendRequestAsync("terrain.resetExaggeration");
        public Task<bool> SetTerrainProviderAsync(string type, string? url = null, string? ionToken = null)
            // Terrain providers perform a real readiness check before they are reported as
            // active. Allow enough time for the first elevation tile/provider metadata to
            // arrive on slower connections instead of incorrectly showing a bridge timeout.
            => SendRequestAsync("terrain.setProvider", new { type, url, ionToken }, timeoutMs: 45000);

        public Task<bool> SetBasemapAsync(string basemapId) => SendRequestAsync("layers.setBasemap", new { mode = basemapId });
        public Task<bool> ToggleHybridLabelsAsync(bool show) => SendRequestAsync("layers.toggleHybridLabels", new { show });
        public Task<bool> AddImageryLayerAsync(string id, string name, string type, string url, string attribution = "", double opacity = 1.0, bool visible = true)
            => SendRequestAsync("layers.add", new { id, name, type, url, attribution, opacity, visible });
        public Task<bool> RemoveImageryLayerAsync(string id) => SendRequestAsync("layers.remove", new { id });
        public Task<bool> SetLayerVisibilityAsync(string id, bool visible) => SendRequestAsync("layers.setVisibility", new { id, visible });
        public Task<bool> SetLayerOpacityAsync(string id, double opacity) => SendRequestAsync("layers.setOpacity", new { id, opacity });
        public Task<bool> ReorderLayerAsync(string id, string direction) => SendRequestAsync("layers.reorder", new { id, direction });
        public Task<bool> SetBordersVisibleAsync(bool visible) => SendRequestAsync("layers.setBorders", new { visible });
        public Task<bool> SetGraticulesVisibleAsync(bool visible) => SendRequestAsync("layers.setGraticules", new { visible });
        public Task<bool> SetGraticulesConfigAsync(bool visible, bool showLabels = true, bool showKeyParallels = true, bool showUtmZones = false,
            bool showLongitudeLines = true, bool showLatitudeLines = true, double longitudeIntervalDegrees = 0.0, double latitudeIntervalDegrees = 0.0, string gridLabelFormat = "DMS", string utmLabelMode = "Both") =>
            SendRequestAsync("layers.setGraticulesConfig", new { visible, showLabels, showKeyParallels, showUtmZones, showLongitudeLines, showLatitudeLines, longitudeIntervalDegrees, latitudeIntervalDegrees, gridLabelFormat, utmLabelMode });
        public Task<bool> SetCreditsConfigAsync(string mode, bool showOptionalLogos) =>
            SendRequestAsync("credits.setConfig", new { mode, showOptionalLogos });
        public Task<bool> SetZoomSensitivityAsync(double factor) =>
            SendRequestAsync("camera.setZoomSensitivity", new { factor });

        public Task<bool> SetReferenceLayerAsync(string id, bool visible, double opacity, string name, string url, string layerType, string attribution, string? wmtsLayer = null, string? dateOrTimestamp = null, string? query = null) =>
            SendRequestAsync("reference.setLayer", new { id, visible, opacity, name, url, layerType, attribution, wmtsLayer, dateOrTimestamp, query });

        public Task<bool> RegisterDynamicBasemapAsync(string id, string name, string url, string attribution = "Esri World Imagery Wayback", bool setActive = false) =>
            SendRequestAsync("layers.registerBasemap", new { id, name, url, attribution, setActive });

        public Task<bool> SetReferenceLayerOpacityAsync(string id, double opacity) =>
            SendRequestAsync("reference.setOpacity", new { id, opacity });

        public Task<bool> SetSearchPinAsync(double longitude, double latitude, string title, bool show = true) =>
            SendRequestAsync("search.setPin", new { longitude, latitude, title, show });

        public Task<bool> StartSwipeAsync(string leftBasemapId, string rightBasemapId) =>
            SendRequestAsync("swipe.start", new { left = leftBasemapId, right = rightBasemapId });

        public Task<bool> StopSwipeAsync() =>
            SendRequestAsync("swipe.stop");

        public Task<bool> SetCompassModeAsync(string mode) =>
            SendRequestAsync("compass.setMode", new { mode });

        public Task<bool> SetSunlightAsync(bool enabled, double timeHours = 12.0, string? dateUtc = null) =>
            SendRequestAsync("environment.setSunlight", new { enabled, timeHours, dateUtc });

        public Task<bool> SetAtmosphereAsync(bool enabled) =>
            SendRequestAsync("environment.setAtmosphere", new { enabled });

        public Task<bool> SetCloudsAsync(bool enabled) =>
            SendRequestAsync("environment.setClouds", new { enabled });

        public Task<bool> SetFlightModeAsync(bool enabled) =>
            SendRequestAsync("flight.setActive", new { enabled });

        public Task<bool> SetNasaGibsImageryAsync(string layer, string date) =>
            SendRequestAsync("historical.nasaSet", new { layer, date }, timeoutMs: 18000);

        public Task<bool> ClearNasaGibsImageryAsync() =>
            SendRequestAsync("historical.nasaClear");

        public Task<bool> PlayGpsTrackAsync(object points, double durationSeconds = 30) =>
            SendRequestAsync("gps.playTrack", new { points, durationSeconds });

        public Task<bool> StopGpsTrackAsync() =>
            SendRequestAsync("gps.stopTrack");

        public Task<bool> PauseGpsTrackAsync(bool paused) =>
            SendRequestAsync("gps.pauseTrack", new { paused });

        public Task<bool> SetTimeOfDayAsync(double timeHours, string? dateUtc = null) =>
            SendRequestAsync("environment.setTimeOfDay", new { timeHours, dateUtc });

        public Task<bool> SetMapCrsAsync(int epsgCode, string name, bool isProjected, string unit) =>
            SendRequestAsync("crs.setMapCrs", new { epsg = epsgCode, name, isProjected, unit });

        public void SimulateCameraMoveEnd(CameraState state)
        {
            state.Sanitize();
            CurrentCamera = state;
            CameraMoveEnded?.Invoke(this, state);
        }
    }
}
