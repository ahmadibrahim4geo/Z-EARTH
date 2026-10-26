---
name: system-capabilities-and-permissions
description: Engineer cross-platform application-to-device capabilities and least-privilege permissions for time, GPS, files, network, sensors, serial, Bluetooth, camera, notifications, and optional trusted external skills with native verification.
license: MIT
compatibility: Cross-platform planning guidance; real OS/device permissions and integrations require target host tools, hardware and native tests.
---

# System Capabilities & Permissions

**Scope:** a shared engineering skill for GIS and non-GIS programs across Windows, macOS, Linux, web/WebView, Qt, Android/iOS and Python. This is NOT a privileged daemon, installed driver, live device probe or permission grant.

**Routing:** [Intent](../prompt-intent-orchestrator/SKILL.md) → [GIS router](../gis-development/SKILL.md) → this specialist **only when needed**. Add [GIS Device Context](../gis-device-context/SKILL.md) for scene-vs-device coordinates, UTC/solar computation and GPS accuracy; [Desktop](../gis-desktop-application-development/SKILL.md) or [Web](../geospatial-web-mapping/SKILL.md) for actual runtime code; [Security Guardian](../gis-security-guardian/SKILL.md) for approvals and trust, [UI/UX](../gis-ui-ux-design-testing/SKILL.md) for consent flows, and [E2E](../gis-desktop-e2e-testing/SKILL.md) for native evidence. No circular routing or loading every adapter by default.

## Implementation process

1. Capture reviewed user goal, target platform/versions, precise requested capability, purpose, privacy sensitivity, input/output, user gesture, offline fallback, and acceptance test.
2. Discover **candidate** platform libraries and external Agent Skills/MCP tools; review origin, installed availability, version, licenses, host tool schemas, permitted destinations and requested privileges. Catalog entries are neither installed nor trusted execution authority.
3. Separate API support, OS/app/browser/user permission, physical device/driver availability, and successful runtime operation. A development plan does not grant GPS, camera, files, network, Bluetooth or admin rights.
4. Define one accountable adapter for each capability. Contract: capability ID, platform, origin/resource scope, availability, permission, expected side effect, value/accuracy/freshness, cancellation, timeout, retry, fallback, sanitized evidence and owner.
5. Use one **platform-appropriate** adapter per action: Windows .NET/WinRT, Qt permissions, browser Geolocation/Permissions API, origin-scoped Electron/WebView2, Tauri v2 capabilities, Linux portals, mobile OS SDK, or optional Python psutil/pyserial/Bleak. Never change the program's renderer or framework merely to read time.
6. Require an appropriate user gesture before sensitive device access; honor denied/revoked/restricted policy, unplugged sensors, stale results and offline state. Never auto-elevate, silently track GPS or export unique identifiers/audio/video/precise coordinates.
7. Test denied/revoked, unavailable device, timeout, connectivity loss, app restart, keyboard/RTL, and packaged permissions. Report T0/T1 offline tests separately from T2 real provider, T3 UI, T4 clean-install acceptance; mark missing native checks NOT RUN.

**States:** Availability UNASSESSED / CANDIDATE / VERIFIED_AVAILABLE / UNAVAILABLE / ERROR. Permission NOT_REQUIRED / NOT_REQUESTED / PROMPTABLE / GRANTED / DENIED / RESTRICTED / REVOKED / UNKNOWN. An offline plan may NEVER claim VERIFIED_AVAILABLE or GRANTED. For date/time, sensor permission is usually NOT_REQUIRED; for location and camera it is not.

**Cost and licensing:** Follow the repository [free-first policy](../../references/free-first-policy.md): prefer no-cost local/native capability adapters and identify paid APIs, device/service quotas and any separately licensed host. Free availability never grants device permissions.

**Boundaries:** this skill owns generic capability specifications and adapter review, Security owns permissions and trust, platform specialists implement actual native APIs, GIS Device Context owns geodetic and time semantics, UX owns user prompts, E2E owns real UI evidence. No unrestricted native bridge, auto-run imported skill or copied secrets.

Read [platform/permission matrix](../../references/system-capabilities-permissions.md), [external skill integration rules](../../references/system-skill-interop.md), [adapter candidate registry](../../references/system-capability-registry.json) and [offline planner](../../scripts/system_capability_check.py). The [example](../../examples/system-capabilities/README.md) and [tests](../../tests/test_system_capabilities_permissions.py) are nonexecuting device fixtures, not live sensor tests.
