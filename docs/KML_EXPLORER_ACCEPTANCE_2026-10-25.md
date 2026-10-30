# Z EARTH — KML/KMZ, Explorer and device-context acceptance matrix
**Audit date:** 2026-10-25 · **Review branch:** `fix/kml-explorer-resize-clear-20261025` · **PR:** #14

## Verification model (do not conflate tests with application behavior)

T0 source routing → T1 markup/script and static contracts → T2 .NET/Node and **actual file-byte readback** → T3 Windows WPF + WebView2 GPU interaction → T4 fresh portable EXE installed profile, cross-session persistence and Google Earth Pro visual side-by-side.

A passing CI workflow confirms **only completed T0–T2 checks**. No Cloud GitHub tool in this review session operates the user's actual physical Windows interactive application. A check labeled T3/T4 is pending until repeatable user/Windows automation evidence exists.

## KML/KMZ file operations and fidelity

| Scenario | Automated code/fixture evidence | Still necessary before native parity claim |
|---|---|---|
| Import KML and nested Folders/Placemark/Path/Polygon/hole | `Sprint4CrsAndKmlTests` parses fixtures | Open in Windows and compare tree counts, coordinates and camera |
| Export Selected Placemark, Path, Polygon | `KmlExportRoundTripRegressionTests`: reopen KML and assert feature geometry | Export actual selected nodes via Explorer button/context menu and reopen in Google Earth Pro |
| Selected child inheriting styles/icon from parent Document/KMZ | `KmlSelectionExport` copies ancestor Style/StyleMap/Schema XML and inherited archive resources; test verifies output subtree | Compare actual imported icons and placements; link fidelity after Save As |
| KML Style, StyleMap normal, LineStyle, PolyStyle, IconStyle | `KmlStyleResolver` reads AABBGGRR (KML) into #RRGGBB (Cesium), opacity, line width and embedded href; fixture tests check exact colors | Inline highlighting/style map hover, scale/anchor `hotSpot`, line stippling, icon rotation and native screenshot comparisons remain |
| Imported KMZ binary assets | `KmzService` keeps resources in imported PlaceItem, preserves paths, writes archive atomically; fixture physically reads reexported ZIP icon bytes | KML icon render through WebView2, models/ground overlays and nested relative hrefs at runtime |
| KML `TimeStamp` / `TimeSpan` | Timestamp original XML, parsed TimeSpan serialized; in-memory and SQLite tests | Test time filtering, `gx:Track` animation and live timeline; no full runtime parity claim |
| KML `MultiGeometry` | Preserve whole source element as XML on round-trip (no duplication); fixture counts Point + LineString | Renderer currently focuses one geometry; multigeometry visual rendering still incomplete |
| KML coordinate precision | Serializer uses IEEE double round-trip formatting rather than F6 or altitude F1; high-precision readback test | Compare real survey-grade test dataset and CRS positions against Google Earth Pro |
| Plain KML vs archived assets | Existing embedded KMZ resources trigger clear KMZ-required warning for KML export | User-visible dialog and complete KMZ export workflow test |
| Unsupported/invalid XML | Serialization fails rather than silently dropping an unknown preserved fragment; existing file not overwritten | Try hostile KML/DTD, malformed XML, corrupt KMZ, missing resources, zip traversal, oversized archive |
| Save As project then Open | SQLite now stores asset dictionary, TimeSpan, radius and flags (old schema loaded via optional column); real file DB roundtrip test | Entire .zearth ZIP GUI save/reopen, crash recovery and legacy fixture comparisons |
| Project format select | Save As advertises `.zearth` only, matching actual ZIP+SQLite writer | UI dialog extension/title default and reopen in clean build |

**Known limits:** There is no certified Google Earth Pro 100% parity. `gx:Track`, NetworkLink refresh/Region/LOD, complex animated KML, full MultiGeometry native view, COLLADA placements, all icon anchor/scale/highlight semantics and licensed provider behavior need dedicated implementation and native data fixtures. Preserving unhandled XML for export does not mean the renderer interprets it.

## Explorer and user experience

- Start Explorer at **450px** wide; sidebar draggable horizontally with upper bound 740px; narrow-window/large-font constraints still need actual DPI/device testing.
- Search Name and Coordinates separately. Run two searches; Clear must remove query, results, selected pin/status and any *late* pending search response must not repopulate the view. New Clear buttons near field and results.
- Drag splitter between Search and My Places vertically; neither expanded child tree height nor number of KML nodes should force the My Places pane to grow outside its assigned row. Tree itself scrolls when large. Check expanded/collapsed behavior and empty-space usage at 100%, 125%, 150%, 200% DPI.
- My Places toolbar wraps to multiple rows instead of hiding **Export Selected Item / Export All / Delete**, and selected node determines export/delete action. Confirm both mouse and keyboard access and contextual commands.
- Verify left/right dock mode, global workspace splitter, collapsed panels, long Arabic item names and theme contrasts. Static XAML parsing cannot prove layout.

## Device time and location — privacy requirements

- **Time:** Z EARTH Sunlight currently uses UTC date from .NET `DateTime.UtcNow` and JS native `Date`, plus user-adjusted UTC time of day. Test with controlled system dates/timezones, off-by-one UTC day and persistence; don't infer real local timezone solely from longitude.
- **Location:** Explicit `My Device Location` button in Search by Coordinates → JS geolocation → WebView2 `PermissionRequested` modal (per-request, not saved) → validated location event → WGS84 EPSG:4326 X=longitude, Y=latitude fields with reported accuracy. Test grant, deny, browser unsupported, timeout, offline, imprecise fix, stale fix, and that **no device fix is sent to search providers or logged without user action**.
- No location permission should be requested on app startup, KML import, map pan, or automatic Sunlight changes. Camera/map center is not automatically physical device location.

## Skills and regression gates

Load `.github/skills/prompt-intent-orchestrator/SKILL.md`, then route through `gis-development` to `kml-kmz-engineering`, `gis-desktop-application-development`, `gis-data-flow-diagnostics`, `gis-desktop-e2e-testing`, `completion-evidence`, `agent-development-journal`, and `gis-security-guardian` only when relevant. Source changes are verified with:

```powershell
python scripts/validate-agent-skills.py
node --test tests/repo-command-contract.test.cjs
dotnet build ZEarth.sln -c Release
dotnet test tests/ZEarth.Tests/ZEarth.Tests.csproj -c Release
.\scripts\create-portable-preview.ps1 -TestsAlreadyPassed
```

View GitHub Actions result on this branch **for the latest commit**. Run T3 real Windows screenshots and manual workflows before claiming final completion; do not merge stacked PRs until dependencies and CI are satisfied.
