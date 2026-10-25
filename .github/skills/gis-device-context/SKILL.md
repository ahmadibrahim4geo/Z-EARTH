---
name: gis-device-context
description: Safely obtain and normalize runtime system time, date, timezone and consented device geolocation for GIS tools, solar sunlight, scene-aware simulation and desktop/web apps, with explicit source accuracy and privacy controls.
license: MIT
---

# GIS Device Context — Time, Location & Runtime Capabilities

**Free-first default:** Follow [no-cost GIS workflow policy](../../references/free-first-policy.md). Prefer platform permission APIs and offline input; do not require paid geolocation, device telemetry or network lookup.

This is a **development guide**, not a background service or a grant of access to device location. An AI agent must never claim to know precise live coordinates or the running device's clock without a real host-provided value and the required user permission.

## Context decision: what location?
1. **Explicit analysis location** provided by user/dataset/scenario always wins for that GIS computation.
2. **Active map scene/camera target or selected feature** can be used when the tool clearly operates on the scene, not the user; record `source=scene`.
3. **Device geolocation** only for an explicit 'my location' request or opt-in feature, through an OS/browser permission API. Permission denial, low accuracy, timeout, unavailable radio/GPS, stale fixes and denied policy are valid outcomes.
4. **Manual coordinates, geocoded chosen place or project default** are visible fallbacks selected by the user. Never silently infer precise coordinates from timezone, locale, IP or browser language; never swap the user's position with map center.

## Temporal correctness
- Capture trusted wall-clock instant as timezone-aware UTC ISO-8601 timestamp; show local date/time using an **explicit IANA timezone** when known, or an identified system UTC offset as provisional. Distinguish *current time* from simulated time, *system timezone* from target-location timezone; check daylight-saving transitions and system clock misconfiguration. For deterministic tests inject a frozen clock.
- Keep live Sunlight mode vs manually scrubbed/frozen date slider distinct; update on tick only in live mode. For animation interval use a monotonic clock; for astronomical computation use a timezone-aware UTC instant.
- Solar azimuth/elevation require **latitude, longitude, UTC instant**; terrain shadowing further needs DEM/horizon, height and appropriate vertical units. Timezone chooses display, not a substitute for longitude. Show local sunrise/sunset only where defined (polar edge cases).
- Never assume Gregorian local calendar labels imply UTC values or that offset `+03:00` uniquely identifies `Africa/Cairo`.

## Implementation adapters
- **Browser/MapLibre/Cesium:** `new Date()` for time; `Intl.DateTimeFormat().resolvedOptions().timeZone` for browser IANA ID when available; `navigator.geolocation.getCurrentPosition` or `watchPosition` only in secure contexts with user consent and timeouts; stop tracking when not needed. Permission may also be blocked by policy. Scene target is independent of GPS.
- **Windows desktop C#/WPF:** `DateTimeOffset.UtcNow`, `TimeZoneInfo`; use Windows.Devices.Geolocation `Geolocator.RequestAccessAsync()` then `GetGeopositionAsync()` from correct UI thread in foreground; packaged/unpackaged capabilities and platform support differ. Integrate with ViewModel and explicit permission control. No silent background scans.
- **Python/QGIS/ArcGIS:** `datetime.now(timezone.utc)`, Python 3.9+ `zoneinfo.ZoneInfo` (+ `tzdata` on systems lacking IANA database); legacy ArcMap Python 2.7 requires a separate compatible approach. The Python standard library **does not read GPS location by itself**; use a platform adapter with permission or explicit inputs.
- **Solar calculations:** use pinned/verified [Astral](https://github.com/sffjunkie/astral) or [pvlib](https://github.com/pvlib/pvlib-python), not a time-to-angle shortcut. Verify units, azimuth convention, precision and tested installation. Libraries are optional, not automatically vendored.

## Minimal context output
Produce a data record with `time_utc`, `display_time`, `timezone_id`, `timezone_source`, `time_mode` (live/simulated), `latitude`, `longitude`, `location_source`, `accuracy_m` (nullable), `location_timestamp` (nullable), `permission_status`, and a freshness/status flag. Null when unavailable; never fabricate coordinates/precision. Include target-area timezone separately when Sunlight operates away from device location. Hardware OS/GPU/memory capability checks are optional only when needed, minimum scope and no unique identifiers.
- Data-minimize: no precise location in analytics, URL, public screenshots, issue reports, Git commits, remote logs or default saved projects. No persistent GPS history by default; disclose opt-in storage/export and retention.
- Provide visible `Use my location`, `Choose point on map`, `Enter coordinates`, `Use current time`, and `Simulate time` affordances as relevant.
- Permission denied -> nonblocking manual mode. Location inaccurate -> show accuracy and confirm use; stale -> refresh or manual. Wrong system clock -> warn and allow manual test instant. Offline mode must remain usable.

## Sources, examples, acceptance
- [Device and solar reference links](../../references/device-context-and-sunlight.md).
- [Offline, injection-friendly Python example](../../examples/device-context/README.md) demonstrates timestamps/timezone, *not* automatic location or complete solar renderer.
- Test DST gap and overlap, dateline, +/-90 latitude, no location, denied permission, low accuracy, stale fix, app restart, suspended/resumed live mode, out-of-area scene, offline mode and daylight/polar cases.
- Native acceptance requires browser user gesture/HTTPS or Windows UI permissions and actual host execution. Static fixtures do not prove GPS hardware support, precise device location, or correct rendered sunlight.
