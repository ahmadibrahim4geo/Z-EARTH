# Engineering investigation: KML/KMZ, Places, layers, search, and host↔globe traffic

Date: 2026-10-26. **Scope:** actual ZEarth main-branch code vs public Google KML behavior/reference application user-facing documentation. User-provided Google Earth installation file names inform reference categories; they do not reveal private Google engine protocols. No new GIS Development Skill is proposed or created.

## A. Observable reference contract: on-demand KML streaming
The public [Google KML Regions guide](https://developers.google.com/kml/documentation/regions) explicitly documents viewport- and LOD-dependent data selection, inherited Regions and NetworkLinks, and SuperOverlays that stream only relevant higher-resolution content at the needed scale. The [KML reference](https://developers.google.com/kml/documentation/kmlreference) defines time- and view-based NetworkLink refresh. These are **published, implementable interoperability contracts**; they do not expose Google's proprietary native globe networking code.

## B. ZEarth: exact Places import → C# → JavaScript path

```text
Import KML/KMZ
  └── MainViewModel.ExecuteImportKml [3343+]
      ├── KmlParser.ParseFile or KmzService.ExtractAndParse
      ├── Add imported PlaceItem tree to Temporary Places
      ├── save session, mark project modified
      └── UpdatePlacesOnGlobe [3107+]
          ├── recurse through MyPlaces/children
          ├── serialize **all visible** Placemark/Path/Polygon/Overlay records into flat list
          └── _ = GlobeBridge.SetPlacemarksAsync(flat) [3271] (not awaited)
              └── correlated request placemarks.set via WebView2
                  └── cesium-engine.js handleHostMessage [3954]
                      └── loadPlacemarks [1085]
                          ├── JSON.stringify entire new array to deduplicate
                          ├── remove all previously displayed placemark entities
                          ├── rebuild all visible entities
                          └── reply completed {count}; requestRender()
```

**Direct evidence:**
- `src/ZEarth.App/ViewModels/MainViewModel.cs:3107–3272`: flat recursive serialization to JS; currently sends whole visible collection. Imports at `3343–3390`.
- `src/ZEarth.Globe/GlobeBridge.cs:714`: `SetPlacemarksAsync(object placemarks)`.
- `src/ZEarth.Globe/Assets/cesium-engine.js:1023–1225`: `lastPlacemarkSignature`, `JSON.stringify(items)`, remove-all/recreate-all, map to Cesium entity types.
- `src/ZEarth.Globe/Assets/cesium-engine.js:3954–3961`: JS action `placemarks.set` accepts a full array.
- `src/ZEarth.Domain/Kml/KmlParser.cs:88–110`: NetworkLink, Region/overlays may be preserved as XML fragments (round-trip preservation ≠ executable runtime support).
- `src/ZEarth.Domain/Kml/KmzService.cs`: ZIP traversal and decompressed size checks.
- `src/ZEarth.App/Views/MainWindow.xaml.cs:328–342,442–462`: post message, trusted-origin readback and dispatch.

**High-confidence architectural performance risk (P0/P1):** even a small edit can serialize/transmit and rebuild the whole collection. The optimization to skip identical JSON strings avoids no-op reloads only. Time and memory cost scale with total visible elements, and Base64-embedded icons may amplify message sizes. No benchmark proves the runtime impact yet.

**Recommended independent improvement:** use stable Place IDs and incremental `placemarks.patch` changes (add/change/remove), batch updates, spatial viewport/LOD filtering, versioned request sequence IDs, cancellation/backpressure and per-ID Cesium entity updates; retain full snapshot only for initial load/recovery. Use realistic 100/1,000/10,000-feature fixture benchmarks and define update latency/CPU/memory budgets before and after. Validate no position/style drift.

## C. Region / NetworkLink / altitude-mode fidelity gaps

| KML feature | Google documented behavior | ZEarth source status | Required check |
| --- | --- | --- | --- |
| Region + min/max LOD | Visible/active by view and screen-space size | No matching viewport-driven Region loader seen in inspected Places pipeline | Synthetic nested Region fixture: turn camera away, verify **no** remote fetch, then zoom into region and load it |
| NetworkLink refresh | Fetch local/remote KML on time/view changes | Parser preserves fragments; active fetch/refresh not demonstrated | Synthetic local HTTPS fixture, view refresh, safe URL allowlist, SSRF and cache protections |
| KML styles/StyleMaps | Map normal/highlight, icon, label and geometries | StyleResolver exists; legacy v2.1 Document fix committed | Semantic roundtrip; compare rendered highlight and icon behavior |
| GroundOverlay | Draped/raster overlay with geographic extent and image | ZEarth flattens GroundOverlay objects into rectangular Cesium image entities | Compare rotated overlays, dateline crossing, transparency, async image failures |
| Altitude modes | clampToGround, relativeToGround, absolute, gx seafloor options | JS `getHeightReference` implements common ground/absolute modes; seafloor modes not independently verified | Separate true bathymetric mode or explicit unsupported banner |
| Import performance | User-visible responsiveness with large datasets | Full array serialization + rebuild | 100→10,000 items, cancellation, edit-one-item latency and memory |

**Important distinction:** published Google KML Regions techniques are directly suitable for an independent implementation, but do NOT mean the private Google Earth terrain server uses the same wire protocol.

## D. ZEarth imagery and external tile traffic
- `src/ZEarth.Globe/Assets/cesium-engine.js:117–750` registers configurable imagery providers using Cesium `UrlTemplateImageryProvider`, `WebMapServiceImageryProvider`, `WebMapTileServiceImageryProvider` and offline Natural Earth; selected layers generate network tile requests from Cesium rather than invoking Google.
- `src/ZEarth.Domain/Imagery/ImageryRegistry.cs` catalogs offline and opt-in web sources. User-facing attribution/status is maintained separately.
- `src/ZEarth.App/Services/WaybackService.cs` obtains Esri Wayback release catalog through .NET HttpClient and passes dated **releases** to globe timeline. These dates must not be represented as acquisition dates of the underlying imagery.
- **Remaining validation:** what loads automatically on startup, rate limits/provider licenses, toggled-off request activity, layer ordering/opacity and 2D/3D lossless state.

## E. Search system
`src/ZEarth.Domain/Search/SearchService.cs` searches My Places/Temporary recursively, then optionally calls the **Photon** online geocoding endpoint with a 4-second HttpClient timeout (five hits), handling Arabic text. Coordinate input also supports multiple projected CRS via PROJ-based transformation.

**Gap:** searching saved local items is not equivalent to a complete **offline worldwide gazetteer**. A full search layer would need a separately licensed place-name dataset, multilingual indexing, geographic filtering, scale-dependent labeling and caching; compare place-name language and relevance, not merely returned list count.

## F. Source-level command bus quality
- From the reviewed `MainWindow.xaml` and `MainViewModel.cs`, the main XAML includes approximately **72 distinct command bindings** whose named command declarations are present in the main viewmodel (including RelayCommand).
- `GlobeBridge` emits roughly **50 unique JSON actions** all with corresponding JavaScript switch cases in the source examined at the time. This is a **static** audit only: a matched action may still fail at execution, time out or render incorrectly.
- The bridge uses request IDs, cancellation and terminal completion; it is *not* a direct C# call to a native Google Earth engine. It is ZEarth's independent WebView2 host protocol.
- Native UIA tests and actual frame/pixel/scene readback remain necessary.

## G. High-value prioritized implementation work
P0: test terrain-based elevation profile, KML Region+NetworkLink streaming in user-owned synthetic data and full-load vs incremental Places performance.
P1: verify rendering of KML overlays/altitude modes and imagery provider request cancellation, terrain/network exceptions, load times and trust boundaries.
P2: internationalized offline gazetteer, documented tours/GPS extensions, printing and UX parity after core functions pass.

**Agent status:** Research above is grounded analysis performed in this conversation using tools. It is **not** the output of independently launched AI agents. GitHub Actions role jobs remain deterministic static checks, not model workers; the current connector has no model-agent dispatch API.
