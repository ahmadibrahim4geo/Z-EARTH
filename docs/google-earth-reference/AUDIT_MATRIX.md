# Reference lab: executable evidence contracts

The twelve roles described in [README](README.md) are **workstreams**, not twelve agents that have already run. This report records current evidence and precise tests; it does not claim runtime verification.

## Evidence from Google Drive (read-only directory metadata, 2026-10-26)
- `client/res/`: `navcontrols.rcc`, `toolbar.rcc`, `leftpanel-layer.rcc`, `search.rcc`, `tmcontrols.rcc`, `tourcontrols.rcc`, `terrainmgr.rcc`, `default_myplaces.rcc`, `userpalette.kml`.
- `client/shaders/`: `atmosphere.glslesf`, `atmosphere.glslesv`, `lighting.h`, `stars.glslesf`, `watersurface.glslesf`, `hammer_aitoff.glslesf`, `viewshed.h`.
- `client/plugins/`: `imageformats/`, `platforms/`, `printsupport/`, `audio/`, `mediaservice/`, `bearer/`, `npgeinprocessplugin.dll`.
- `client/`: `Qt5Gui.dll`, `Qt5Widgets.dll`, `Qt5OpenGL.dll`, `Qt5Network.dll`, `Qt5Positioning.dll`, `Qt5WebKit.dll`, `qt.conf`, `drivers.ini`.

**Interpretation limit:** a resource name does not establish an implemented command, algorithm, or quality level. Never copy Google's resources, shaders, DLLs, or executable into this repository.

## ZEarth baseline from existing project documentation
- `src/ZEarth.App/`: WPF MVVM commands and settings.
- `src/ZEarth.Globe/`: CesiumJS/WebView2 bridge.
- `src/ZEarth.Domain/`: KML/KMZ, GDAL/PROJ, domain data.
- `tests/ZEarth.Tests/`: tests may use `FakeGlobeEngine` and therefore do not prove native globe behavior.
- Existing contracts: `docs/GOOGLE_EARTH_PRO_PARITY.md`, `docs/CLEAN_ROOM_PARITY_PILOT.md`, `docs/FRESH_CLONE_VALIDATION.md`.

## Audit tickets / owners / acceptance evidence

| ID | Owner | Investigation | Required proof | Status |
|---|---|---|---|---|
| R01 | Reference installation | Classify installer files, metadata and provenance | File manifest, versions, hashes if available | PARTIAL: folder names only |
| R02 | Reference UI | Enumerate visible menus/dialogs and keyboard shortcuts | Native screen recording and step log | NOT VERIFIED |
| R03 | Reference navigation | Camera, compass, north, mouse gestures | Repeatable desktop action trace | NOT VERIFIED |
| R04 | Reference rendering | Atmosphere, sunlight, stars, terrain | Visual scenarios, timing and GPU conditions | NOT VERIFIED |
| R05 | Reference interoperability | KML/KMZ placemark, folder, styles, altitude | Public standards + round-trip fixtures | NOT VERIFIED |
| R06 | Reference grid | Meridians, parallels, labels, major lines | State-transition screenshots | NOT VERIFIED |
| R07 | Reference search | Coordinates, placenames, provider/network behavior | Online/offline scenario log | NOT VERIFIED |
| R08 | Reference reliability | Startup, recovery, persistence, resources | Repeated launch/save/reopen trace | NOT VERIFIED |
| Z01 | ZEarth UI | WPF ribbon command-to-handler audit | No inert buttons, handler tests, UI evidence | NOT VERIFIED |
| Z02 | ZEarth globe | WebView2 bridge, 2D/3D, compass | Native GPU/WebView2 readback | NOT VERIFIED |
| Z03 | ZEarth data | KML/KMZ import/export and Places | Fixture diff + state persistence | NOT VERIFIED |
| Z04 | ZEarth features | Grid/sunlight/search and performance | Negative tests + telemetry | NOT VERIFIED |

## First actionable regression scenarios
1. **Grid OFF:** switch off Show Grid, meridians, parallels, major geographic lines and labels. Verify *all* primitives disappear, including at the poles, after zoom and 2D/3D switching. No stale overlay.
2. **Sunlight OFF:** enable sunlight, move time/date, disable it. Verify illumination returns to baseline; repeated toggles do not create duplicated controllers.
3. **KML round trip:** nested folders, Unicode name, visibility, style, altitude, camera and icon references; export/reimport, compare semantic values, log unsupported properties rather than silently discarding.
4. **Camera north:** test north reset, 2D/3D, poles, dateline, previous/next view, reopen; preserve ZEarth-only modes.
5. **Search offline:** confirm local results and explicit provider errors; no fabricated results or uncontrolled retry storms.

## Execution gate
Run source and unit checks first, then Windows native WPF/WebView2 GPU tests with screenshots/logs. Only change a ticket to PASS when attached evidence exists. Distinguish a source review from real reference observation.
