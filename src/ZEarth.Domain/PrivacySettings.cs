using System;

namespace ZEarth.Domain
{
    /// <summary>
    /// Governs application privacy guarantees, ensuring zero unsolicited network calls (ZE-022, AC-001).
    /// </summary>
    public sealed class PrivacySettings
    {
        /// <summary>
        /// Explicit user opt-in required before any external IP geolocation request is made.
        /// By default, this is FALSE (strict offline privacy-preserving).
        /// </summary>
        public bool EnableIpGeolocation { get; set; } = false;

        /// <summary>
        /// Default startup home preset used when IP geolocation is not enabled.
        /// </summary>
        public HomePreset StartupHomePreset { get; set; } = HomePreset.EgyptAfrica;

        /// <summary>
        /// Checks whether external network requests are permitted for geographic auto-detection.
        /// </summary>
        public bool CanAttemptNetworkGeolocation() => EnableIpGeolocation;
    }
}
