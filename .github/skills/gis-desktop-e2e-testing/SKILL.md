---
name: gis-desktop-e2e-testing
description: Run evidence-driven end-to-end GIS desktop application tests on a real supported OS, including Windows UI Automation, WPF/WinUI/Qt widgets, WebView2 bridges, native map gestures, project persistence, imports, exports and installers.
license: MIT
compatibility: Windows UI Automation and WebView2 acceptance require an interactive Windows desktop session and the matching app/runtime; read-only test design is cross-platform.
---

# GIS Desktop End-to-End Testing

**Purpose:** observe what a *real user* can do in an installed/running GIS application, not what code or a green CI status suggests. Use for UI regressions, unresponsive commands, save/reopen failures, 2D/3D navigation, WebView2 and deployment QA.

**Free-first source and testing policy:** follow the [repository free-first and licensing policy](../../references/free-first-policy.md). Reuse free/offline tools where feasible, disclose any paid GIS host/OS dependencies, and verify each upstream license before copying code. Never silently require a commercial service.

## Route and preflight

1. Start after [Intent Orchestrator](../prompt-intent-orchestrator/SKILL.md) and the [GIS Router](../gis-development/SKILL.md) have identified the user's goal and reviewed scope. Respect [Security Guardian](../gis-security-guardian/SKILL.md). For new desktop architecture also read [Desktop Development](../gis-desktop-application-development/SKILL.md).
2. Read [shared quality handoffs](../../references/gis-ui-quality-coordination.md), [native E2E playbook](../../references/gis-desktop-e2e-testing.md) and [upstream test tools](../../references/gis-ui-quality-upstream-sources.md).
3. Record OS/build number, app commit/build, GIS host/engine, graphics backend/GPU, WebView2 runtime, language, display scale/DPI, test user, map/DEM fixture, installation method and timeouts. If the compatible GUI host is absent, **mark native cases NOT RUN**, never PASS.
4. Choose driver by application shell, **not** project name: FlaUI (.NET Windows UIA2/UIA3), pywinauto (Python Windows), Appium Windows Driver only when available and supported, pytest-qt (Qt widget/unit integration), Playwright (web DOM/eligible WebView2 DevTools). A WebView2 embedded map may require *both* native UIA and a restricted, transient browser debugging connection.
5. Execute complete scenarios from the [E2E scenario catalog](../../examples/desktop-e2e/README.md): launch → open/import → verify layer, icon and CRS → navigate → modify UI → save → close → reopen → export → independently reopen output → inspect errors and clean shutdown.
6. Assert **observable result** (UI tree, map state, output file/data, persisted project state) and never rely exclusively on click success, a green toast, a mock, or rendered screenshot.
7. Distinguish missing AutomationId/accessible name, unavailable window handle, offscreen/hidden state, blocked WebView2 DevTools, engine bridge latency, renderer failure and GIS data loss. Hand dead controls to [Command Audit](../gis-ui-command-audit/SKILL.md) and data mismatch to [Data Flow Diagnostics](../gis-data-flow-diagnostics/SKILL.md).
8. Capture redacted UIA element tree, screenshot when authorized, sanitized trace ID, test logs, versions, repro steps, failure classification, cleanup and rollback evidence.

## Security and coverage

Do not turn on remote debugging on public interfaces or leave DevTools endpoints running after tests. Prefer loopback-only, short-lived, host-authorized test sessions. Prevent credential capture and disclosure of user map extents or private GIS datasets in screenshots/logs. Use deterministic local fixtures and an isolated test user. Never silently mutate production GIS projects. Test RTL and keyboard access via [UI/UX skill](../gis-ui-ux-design-testing/SKILL.md).

**Acceptance:** T0 manifests/tests specified; T1 deterministic mocks/parsers; T2 native GIS engine; T3 actual GUI interaction on target OS; T4 clean-machine installer/recovery. Report the exact tier observed and NOT RUN for every unavailable tier. The [Windows executable test matrix](../../examples/desktop-e2e/README.md) is a specification and can be adopted into a target app's actual test harness.

## Native device and permission acceptance

For features routed through [System Capabilities & Permissions](../system-capabilities-and-permissions/SKILL.md), verify real user gesture, OS/browser prompt, allow/deny, revocation, absent GPS/serial/BLE, timeout, resume and package manifest on the target host. Offline planner output is NOT T3/T4 evidence of any device access. Mark real-platform checks NOT RUN when unavailable.
