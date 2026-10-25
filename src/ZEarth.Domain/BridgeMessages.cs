using System;
using System.Text.Json.Serialization;

namespace ZEarth.Domain
{
    public sealed class BridgeRequest
    {
        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion { get; set; } = "1.0.0";

        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = Guid.NewGuid().ToString("N");

        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        [JsonPropertyName("payload")]
        public object? Payload { get; set; }
    }

    /// <summary>
    /// Correlated bridge request lifecycle statuses (RC-3). JS may acknowledge a long-running
    /// request with <see cref="Accepted"/> and later resolve it with a terminal status.
    /// </summary>
    public static class BridgeStatus
    {
        public const string Accepted = "accepted";
        public const string Completed = "completed";
        public const string Cancelled = "cancelled";
        public const string Failed = "failed";

        /// <summary>Terminal statuses resolve the pending host waiter.</summary>
        public static bool IsTerminal(string? status)
            => string.IsNullOrEmpty(status)
               || string.Equals(status, Completed, StringComparison.OrdinalIgnoreCase)
               || string.Equals(status, Cancelled, StringComparison.OrdinalIgnoreCase)
               || string.Equals(status, Failed, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Diagnostic information about a bridge request that did not complete successfully.</summary>
    public sealed class BridgeFailureInfo
    {
        public string RequestId { get; init; } = string.Empty;
        public string Action { get; init; } = string.Empty;
        public string Reason { get; init; } = string.Empty;
        public bool TimedOut { get; init; }
    }

    public sealed class BridgeResponse
    {
        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion { get; set; } = "1.0.0";

        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = string.Empty;

        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }

        [JsonPropertyName("payload")]
        public object? Payload { get; set; }
    }

    public sealed class CameraChangedNotification
    {
        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion { get; set; } = "1.0.0";

        [JsonPropertyName("event")]
        public string Event { get; set; } = "camera.changed";

        [JsonPropertyName("camera")]
        public CameraState Camera { get; set; } = new CameraState();
    }

    public sealed class FlyToPayload
    {
        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        [JsonPropertyName("altitude")]
        public double Altitude { get; set; }

        [JsonPropertyName("heading")]
        public double Heading { get; set; } = 0.0;

        [JsonPropertyName("pitch")]
        public double Pitch { get; set; } = -90.0;

        [JsonPropertyName("durationSeconds")]
        public double DurationSeconds { get; set; } = 1.5;
    }

    public sealed class SetSceneModePayload
    {
        [JsonPropertyName("mode")]
        public string Mode { get; set; } = "Scene3D"; // Scene3D, Scene2D, ColumbusView
    }

    public sealed class MapCenterInfo
    {
        [JsonPropertyName("valid")]
        public bool IsValid { get; set; } = true;

        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        [JsonPropertyName("altitude")]
        public double Altitude { get; set; }
    }

    public sealed class CursorPositionInfo
    {
        [JsonPropertyName("valid")]
        public bool IsValid { get; set; } = true;

        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        [JsonPropertyName("altitude")]
        public double Altitude { get; set; }
    }
}
