# GE parity 01 — source command inventory and pilot traces

**Date:** 2026-10-28  
**Scope:** tracked `src/` UI declarations in the canonical Git checkout  
**Evidence:** T0/T1 source inventory only unless a separate native report is linked

## Static inventory

The repository scanner now ignores generated `bin/`, `obj/`, `node_modules/`,
cache and journal directories, and recognizes `IsChecked` state bindings in
addition to `Command` and `Click` handlers.

| Result | Count |
|---|---:|
| UI source files scanned | 19 |
| Controls found | 242 |
| Declared command/click/state binding | 183 |
| No binding detected | 34 |
| HTML control requiring manual/dynamic inspection | 25 |
| Parse errors | 0 |
| Duplicate declared IDs | 0 |
| Missing local icon assets | 0 |

Machine-readable evidence: [GE_PARITY_01_COMMAND_INVENTORY.json](GE_PARITY_01_COMMAND_INVENTORY.json).

`NO_BINDING` is a review queue, not a dead-command verdict. Dialog buttons can
use `DialogResult`, code-behind behavior or container semantics not resolved by
this conservative scanner. Each advertised command still requires a manual
handler trace and, where user-visible, native UI acceptance.

## Pilot traces

### Geographic grid — `VIEW-006`

`MainWindow.xaml:596` → `ToggleGraticulesCommand` →
`MainViewModel.cs:1560` → `GlobeBridge.SetGraticulesConfigAsync` / visibility
request → `layers.setGraticulesConfig` → `GraticuleManager` → Cesium data source,
labels and UTM entities → persisted settings and WPF ribbon state.

Current classification: `PRESENT_UNVERIFIED` in the new ledger. During this
pass the canonical settings, bridge defaults, Cesium fallbacks and Options UI
defaults were found to disagree. They now use the approved set
`#FF5252/#3BA9FF/#3AD17B/#E8E8E8/#FFC107`, with a cross-layer regression test.
Existing source, tests and native screenshots still need current-revision
interaction/persistence acceptance before promotion to `PRESENT_VERIFIED`;
T4 remains not run.

### Save Image / screenshot — `FILE-005`, `ADV-008`

`MainWindow.xaml:1088` → `TakeScreenshotCommand` →
`MainViewModel.ExecuteTakeScreenshotAsync` →
`MainWindow.CaptureMapScreenshotAsync` → WebView2 `CapturePreviewAsync` → PNG
file size check and user status.

Current classification: `PARTIAL`. The command captures the current WebView2
viewport. Its English/Arabic label, tooltip and completion status now state that
truthfully and no longer claim high-resolution export. Layout composition,
explicit output size, preview and current-revision native file inspection remain
open. `tests/screenshot-contract.test.cjs` protects the truthful contract at T1.

### Flight navigation — `TOOLS-009`

`MainWindow.xaml:464` → `ToggleFlightModeCommand` →
`MainViewModel.ExecuteToggleFlightModeAsync` →
`GlobeBridge.SetFlightModeAsync` → `flight.setActive` →
`ZEarthFlightSimulator` → Cesium camera and HUD → `flight.changed` event → WPF
toggle state.

Current classification: `PARTIAL`. The implementation is an independent basic
camera-flight mode. Its HUD now explicitly says it is not an aircraft simulator,
and exposes speed, ellipsoid altitude, terrain-relative AGL when available,
throttle, heading, pitch, bank and vertical speed. Keyboard steering, throttle,
pause and exit paths are covered by the mock Node controller test. It has no
aircraft dynamics and no current-revision T3 WebView2/GPU acceptance.

## Verification snapshot

| Gate | Result | Evidence level |
|---|---|---|
| Functional-spec parser | 4/4 passed | T1 |
| UI quality/audit helpers | 18/18 passed with workspace-local Temp | T1 |
| Full Node source/behavior suite | 67/67 passed | T1/T2 mock only |
| Release solution build | 0 warnings, 0 errors; single-node workaround for .NET 10/MSBuild IPC | T1 |
| MSTest | 262/262 passed outside sandbox (testhost named-pipe IPC is blocked inside sandbox) | T2 |
| Native WebView2/GPU: flight | Limited pass: activation/HUD, Page Up throttle 55%→63%, focused Esc exit. Stale WPF OFF status was fixed and passed T1/T2; post-fix T3 recheck blocked by unrelated Filee UAC. P pause not visibly confirmed. | T3 partial |
| Native WebView2 screenshot | Two inspected 910×600 PNG viewport captures, including active flight HUD | T3 partial |
| Packaged Preview | NOT CREATED | T4 not established |

Native artifacts and hashes are recorded in
[`native-evidence/ge-parity-pilot-2026-10-28/README.md`](../native-evidence/ge-parity-pilot-2026-10-28/README.md).

## Next repair order

1. Define and implement composed Export Map Image separately from the truthful
   Quick Screenshot command (layout, resolution, preview and metadata).
2. Run native flight/screenshot acceptance, then decide whether an aircraft
   model is product scope or the camera mode remains deliberately lightweight.
3. Resolve each remaining `NO_BINDING`/`UNINSPECTED` entry against source and
   attach the corresponding functional-spec ID.
4. Run real Windows UIA + WebView2 scenarios and update the 151-item ledger with
   observed evidence rather than inferred completion.

