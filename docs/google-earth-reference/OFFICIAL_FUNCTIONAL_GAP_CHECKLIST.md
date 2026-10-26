# Functional reference checklist — official Google Earth Pro behaviors versus ZEarth

**Reviewed 2026-10-26.** This checklist broadens the reference study beyond the illustrative Clouds example. **Sources:** official Google Earth Help documentation and read-only ZEarth files. Official documentation establishes that Google Earth has a named feature; it does NOT prove the user-provided installation is working, and source implementation does NOT prove ZEarth is working.

## Observable behavior contracts from Google's official help

| Feature | Official reference evidence | ZEarth source inspection | Gap classification | Independent next test or implementation |
| --- | --- | --- | --- | --- |
| Shortcut-driven placemarks, folders, paths, polygons, overlays, models | [Keyboard shortcuts](https://support.google.com/earth/answer/148114?hl=en), e.g. Ctrl+Shift+P/N/T/G/O/M | MainViewModel has Place/Draw/OverlayImage/Model3D commands | **Common, runtime unverified** | Build UIA tests for shortcut and button parity, undo/redo, edit and save |
| Geographic grid | [Keyboard shortcuts](https://support.google.com/earth/answer/148114?hl=en), Ctrl+L | WPF Show Grid and Grid Options; Cesium independent meridian/parallel intervals, UTM zones, major circles | **ZEarth extended capability** | Preserve extra controls, verify no persistent overlays and polar artifacts |
| Compass and camera controls | [Keyboard navigation](https://support.google.com/earth/answer/148115?hl=en): north N, tilt U, recenter R | Reset North, pitch, camera and 2D/3D mode handlers | **Common / ZEarth extra 2D modes** | Test native WebView2 readback, polar/dateline, zoom and view history |
| Layer selection | [Layer overview](https://support.google.com/earth/answer/148130?hl=en): borders, labels, transportation, places, 3D buildings, photos, terrain | ZEarth imagery/layers, terrain and grid options present; specific coverage varies by licensed provider | **Partial, provider data gap** | Inventory each category, offline alternative and its terms, UI labels and attribution |
| Actual flight simulator | [Official flight simulator instructions](https://support.google.com/earth/answer/148089?hl=en): Tools menu, F-16/SR22 | No Flight Simulator command found in MainViewModel/MainWindow; Google installation flight config includes aircraft and controls | **Missing optional capability** | Independently design free simulator (controls, camera, HUD), do not copy copyrighted aircraft assets |
| GPS import and time-tagged tracks | [Import GPS](https://support.google.com/earth/answer/148095?hl=en): tracks and altitude choices | ZEarth has vector imports and KML geometries; native GPS import dialog and KML Track handling not verified | **Review gap** | Test GPX/track timestamps and elevation; show valid lossless conversion limitations |
| Print | [Keyboard shortcuts](https://support.google.com/earth/answer/148114?hl=en): Ctrl+P | ZEarth map screenshot exists; no PrintCommand identified in reviewed MainViewModel | **Potential missing capability** | Independent print/page-layout and scale tests, avoid inferring screenshot equals print |
| Historical imagery | [Google Earth imagery timeslider](https://support.google.com/earth/answer/148094?hl=en) | ZEarth Esri Wayback **release-date** timeline already implemented | **Different dataset and timeline meaning** | Do not label release date as source acquisition date; add only public licensed source dates |
| Historical imagery age and source | [Official acquisition date interpretation](https://support.google.com/earth/answer/6327779?hl=en) | ZEarth source/date attribution varies by provider | **Data provenance gap** | Explicitly label capture date, publishing date, and missing metadata |
| KML from reference resources | User-provided installation userpalette.kml uses legacy namespace 2.1 | ZEarth parser initially missed 2.1 Document; independent namespace fix and synthetic tests now added | **Confirmed defect corrected in source** | Windows build + semantic roundtrip for Style/StyleMap, icons, folders |
| Tours, sunlight and date controls | [Google Earth release documentation](https://support.google.com/earth/answer/40901?hl=en) documents tours and time-based KML | ZEarth has sunlight but no tour playback verified in inspected UI | **Mixed / possible missing feature** | Independent KML gx:Tour playback interpreter with restricted asset loading |
| Clouds and atmosphere | Source installation includes atmosphere/stars shaders; user reported clouds | ZEarth has atmosphere/sunlight and newly added **offline illustrative** 3D Clouds | **Example only, not principal scope** | Separate illustrative graphics from licensed observed cloud data |
| Save and reopen Places/KML/KMZ | [Google support](https://support.google.com/earth/answer/166438?hl=en) | ZEarth MyPlaces/Temporary, KmlParser, KmlSerializer, KmzService | **Common, robustness tests needed** | Preserve nested styles, Unicode, icons, altitude, user settings; run close/reopen tests |

## Cross-product engineering priorities

- **P0 correctness:** real KML/KMZ round-trips and legacy namespace, native grid/label/off-state and command binding consistency.
- **P1 interaction/reliability:** camera motions, 2D/3D and compass options, UI command readback, offline startup/search, save/reopen and crash recovery.
- **P2 documented functional gaps:** GPS KML Track handling, tours, optional print/page-layout, and layer-source provenance.
- **P3 optional extensions:** flight simulator, licensed live meteorological raster overlays, open tree/building 3D data.

## Keep the ZEarth differentiation
Do NOT remove: selectable 2D/3D/Columbus rendering modes where stable, configurable compass, advanced per-axis grid intervals, major named meridians/parallels, UTM overlay, CRS-aware search, free-first basemaps, dark UI and Arabic/English localization. A reference requirement should not erase deliberate independent ZEarth features.

## Truth in evidence
Official docs plus text source constitute **reference specification + code review**, not a real installed-Google-Earth-versus-running-ZEarth benchmark. Twelve CI jobs are reproducible audit roles, not twelve self-directed AI workers. Live behavior must be captured on authorized Windows in controlled sessions before signoff.
