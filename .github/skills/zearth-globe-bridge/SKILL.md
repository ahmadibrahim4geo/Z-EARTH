---
name: zearth-globe-bridge
description: Use this skill to change Globe viewer, camera, WebView2 or message contracts.
---
# CesiumJS and WPF WebView2 bridge
- Web frontend is bundled/local and uses a versioned TypeScript/JSON schema.
- On .NET side validate source origin, schemaVersion, message.type, requestId, payload bounds and errors.
- Expose no generic host calls or filesystem object to JavaScript.
- Every request has one acknowledged response or explicit timeout/cancellation.
- Avoid default Cesium ion access token/paid remote dependencies; show ellipsoid globe offline.
- Add unit tests for malformed messages, flyTo and camera changed round-trips.

## Output
Report changes, exact tests/results, potential data loss, security, licensing, remaining limitations, and the next backlog task.
