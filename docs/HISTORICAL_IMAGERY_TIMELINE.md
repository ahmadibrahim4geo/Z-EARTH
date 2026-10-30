# Historical Imagery — compact in-globe Wayback timeline

**User experience:** Historical Imagery ribbon button opens a small, nonblocking time slider inside the globe viewport. Pan, rotate, zoom and inspect while the slider remains visible. Older/newer and first/latest buttons, a year dropdown, exact date picker, and drag-to-scrub are included. Close or Esc restores the basemap that was active before opening history.

## Wiring

`MainViewModel.HistoricalImageryCommand` → `.NET WaybackService.GetReleasesAsync` → `GlobeBridge.SendRequestAsync("historical.loading/show/error")` → `cesium-engine.js` → `historical-timeline.js` → `ImageryManager.registerDynamicBasemap`. The legacy WPF `HistoricalImageryDialog` is no longer opened by the ribbon.

The public configuration `waybackconfig.json` is fetched via HTTP in .NET. Its official `itemURL` and `releaseDateLabel` properties are parsed; remote URLs are constrained to HTTPS under the trusted Esri Wayback maptiles hosts. Older WMTS `{level}/{row}/{col}` placeholders are converted to Cesium's `{z}/{y}/{x}`. Malformed catalogs, missing network access or service failures result in a visible error, **not fabricated fallback dates**. Rapid slider scrubbing is debounced.

## What the dates mean

**An Esri World Imagery Wayback release is a snapshot of the published basemap**, not necessarily an aerial or satellite acquisition date. Image capture time varies by tile and geographic area. A single archived release may contain mosaics from several acquisition dates. User-entered days without a corresponding Wayback release snap to the nearest real release on or before that day (or the oldest one if earlier than the archive).

Google Earth Pro has a different proprietary historical imagery catalog; no claim of one-to-one dates or Google imagery redistribution. Esri service availability, local coverage, rate limits, attribution and licensing remain external; a successfully sent map command is **not** proof tiles loaded.

## Test contract

- **T1:** Node.js syntax and unit tests for release sorting/selection, invalid dates and unsafe tile URLs, plus XAML and skill static checks.
- **T2:** Windows .NET 10 MSTest fixture verifies the documented Esri Wayback configuration keys and WMTS URL translation.
- **T3 (required, not proven by CI):** Launch native Windows WebView2 and confirm timeline stays inside map, keyboard controls work, camera pan/zoom operates while visible, actual imagery changes at locally available dates, network failures surface correctly, attribution remains visible, and closing returns the old basemap.
- **T4:** Confirm a clean portable build includes historical-timeline.js and UI works without stale local state.

References: [Google Earth Pro historical imagery](https://support.google.com/earth/answer/148094?hl=en), [Esri Wayback app](https://github.com/Esri/wayback), [Esri wayback-core](https://github.com/Esri/wayback-core).

## Imported Earth reference and agent routing (2026-10-25)

The [GIS Development router](../.github/skills/gis-development/SKILL.md) routes this work through `geospatial-web-mapping` and `geospatial-api-integration`, together with the [blog-derived engineering reference](../.github/references/google-earth-pro-engineering.md) and [multi-provider timeline design](../.github/references/historical-imagery-timeline.md). These references are specifications, **not** a replacement for the actual Esri Wayback behavior above. For this app, keep release date and image acquisition date distinct; do not label a Wayback release as a known sensor capture date. Check request failure, viewport usability and provider credits in a real installed Windows/WebView2 build.
