using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ZEarth.Domain;
using ZEarth.Globe;

namespace ZEarth.Tests
{
    /// <summary>
    /// A well-behaved stand-in for the Cesium engine used by bridge dispatch tests.
    ///
    /// It records every outbound host message and immediately answers each correlated request with a
    /// terminal success response. Two consequences matter:
    /// <list type="bullet">
    /// <item>tests no longer have to sit through the real 8 s request timeout, and</item>
    /// <item>assertions are immune to follow-up protocol traffic such as the host's
    /// <c>request.cancel</c> notification that is emitted when a request times out (RC-3).</item>
    /// </list>
    /// </summary>
    internal sealed class FakeGlobeEngine
    {
        private readonly GlobeBridge _bridge;

        private FakeGlobeEngine(GlobeBridge bridge) => _bridge = bridge;

        /// <summary>Every message the host posted to the engine, in order.</summary>
        public List<string> Sent { get; } = new();

        public static FakeGlobeEngine Attach(GlobeBridge bridge, Func<string, bool>? autoComplete = null)
        {
            var engine = new FakeGlobeEngine(bridge);
            bridge.SendJsonToWebView = json =>
            {
                engine.Sent.Add(json);
                engine.MaybeComplete(json, autoComplete);
                return Task.CompletedTask;
            };
            return engine;
        }

        /// <summary>The most recent outbound message whose action matches <paramref name="action"/>.</summary>
        public string LastPayloadFor(string action)
            => Sent.LastOrDefault(m => m.Contains($"\"{action}\"")) ?? string.Empty;

        public bool Any(string fragment) => Sent.Any(m => m.Contains(fragment));

        private void MaybeComplete(string json, Func<string, bool>? autoComplete)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("requestId", out var idProp)) return;
                if (!root.TryGetProperty("action", out var actionProp)) return;

                var action = actionProp.GetString() ?? string.Empty;
                if (action == "request.cancel") return;
                if (autoComplete != null && !autoComplete(action)) return;

                _bridge.HandleWebMessage(JsonSerializer.Serialize(new BridgeResponse
                {
                    RequestId = idProp.GetString()!,
                    Action = action,
                    Status = BridgeStatus.Completed,
                    Success = true
                }));
            }
            catch
            {
                // A test stub must never fault the test.
            }
        }
    }
}
