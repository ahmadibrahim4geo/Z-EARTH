# Z EARTH — Source, command and runtime-contract audit
**Date:** 2026-10-25  
**Audit base:** `fix/terrain-graticule-native-contract-20261025` (PR #11; latest base commit `12c50a52345f4da4ede21073a3ebdac2fb8a427b`)  
**Follow-up branch:** `audit/settings-bridge-workflow-20261025`

## Method

Review is modeled on ISO/IEC 25010 quality dimensions (functional suitability, reliability, usability, security, maintainability), Microsoft WPF/MVVM and WebView2 host security guidance, and applicable OWASP ASVS 5 principles (validation, trusted origin, safe persistence). Source discovery followed:

`MainWindow.xaml binding → MainViewModel command/property → SettingsService/GlobeBridge → cesium-engine.js action → Cesium scene → acknowledgement and UI status`.

References: [Microsoft WebView2 security](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security), [OWASP ASVS](https://asvs.owasp.org/), [project quality gates](../.github/docs/QUALITY_BENCHMARK.md).

## Confirmed defects corrected in this branch

| ID | Evidence / original failing behavior | Implemented correction | Regression/evidence |
|---|---|---|---|
| ZE-AUD-01 | `OptionsDialog.OnSaveClick` edited settings, while `SettingsService.SaveSettings` immediately called `SyncFromDomain`, replacing new snapping fields with stale singleton values | Model-first serialization; `OptionsDialog` explicitly synchronizes domain before persistence; isolated file store writes a temporary file then replaces the target | `SettingsModelSerializationTests`; actual disk file round trip and sensitive-token exclusion |
| ZE-AUD-02 | `App.OnStartup` hard-coded `dark` and `en` unless CLI flags supplied; saved theme/language were never consulted | Read stored preferences with optional CLI overrides; preserve fresh-install defaults | Settings round-trip test; Windows startup still requires T3 |
| ZE-AUD-03 | Options changed `Theme` and `Language` in JSON without invoking Theme/Localization services or globe theme | Apply selected appearance after dialog; subscribe to theme/language events and persist interactive changes | Static bridge policy contract; WPF UI T3 pending |
| ZE-AUD-04 | `WebMessageReceived` forwarded commands without a native sender-origin check; navigation/popup handling was unrestricted | Allow only bundled `https://cesium.local/index.html` or root, reject other sender URLs, block unknown top-level navigation/popups | `WebViewTrustPolicyTests` hostile URI fixtures |
| ZE-AUD-05 | Political Borders checkbox emitted direct `layers.setBorders`, which did not exist in the Cesium JS switch | Proper correlated GlobeBridge action + opt-in Esri reference overlay, with failure rollback, persistent setting and startup restore | `repo-command-contract.test.cjs`; online tiles/native display T3 pending |
| ZE-AUD-06 | Imagery add/remove/visibility/opacity/reorder sometimes responded success even when their underlying operations returned false | Return failed correlated responses for failed layer changes | `repo-command-contract.test.cjs`; bridge failure behavior |
| ZE-AUD-07 | Saved scene view changed scene mode/basemap through unawaited tasks, then flew camera immediately | Await both bridge acknowledgements, stop on failed command before flying | Source contract test. **Mode morph visual completion is still T3 pending** |

## Coverage and unresolved issues

- `repo-command-contract.test.cjs` tracks literal GlobeBridge action names to Cesium receivers, direct C# actions, WPF ribbon command members and trusted-message gate. This finds disconnected actions but is **not** an end-to-end UI test.
- GIS and KML pipelines, CRS transformations, geodesic operations and snapping require **fixture comparison against real outputs**, not mocks alone. Validate imported KML/KMZ styles and coordinate conversion in a Windows session.
- PR #11 has DEM and adaptive grid source improvements. These still require real WebView2 + GPU + terrain tiles, terrain exaggeration comparisons and UTM labels at different zoom levels. Do not equate network tile readiness or Node success with rendered relief.
- Online Esri boundaries depend on provider availability, attribution and applicable service terms. A successful layer registration is not evidence that all requested tiles loaded.
- Native geographic display in both UI languages remains a separate functional and localization acceptance requirement; much of the current XAML has hard-coded English content.
- Acknowledged `scene.setMode` requests can precede Cesium morph completion. Full scene restore needs a native transition-complete gate in a separate focused change.

## Acceptance levels (project T0–T4)

- **T0 source trace:** complete for affected call paths.
- **T1 static:** JavaScript syntax/command and WPF XML checks.
- **T2 component:** Windows .NET test suite, Node regression and portable CI builds; check actual GitHub Actions result for this PR.
- **T3 native:** **NOT EXECUTED HERE**; real Windows WebView2/GPU interaction, actual network tile rendering, screenshot/status cross-checks.
- **T4 clean install:** **NOT EXECUTED HERE**; fresh-profile portable EXE and saved project/KML round-trip.

## Follow-up order

1. Finish PR #11 native terrain/grid acceptance and use the same test protocol for this PR.
2. Confirm settings: change snapping/unit/theme/Arabic/UI grid, close Options, restart and compare against saved values; verify with an actual saved project.
3. Confirm WebView2 allowed/blocked navigation under remote or malformed KML hyperlinks, without disabling legitimate imagery subresource fetches.
4. Check political borders on/off in both offline and online states, with provider error feedback and source attribution.
5. Complete localization coverage, camera mode morph completion and actual KML/CRS round-trip functional tests before production release.

This report is a scoped **source and CI audit**, not a statement that every application feature was tested or fixed.
