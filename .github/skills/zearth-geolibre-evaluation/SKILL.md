---
name: zearth-geolibre-evaluation
description: Consult when evaluating GeoLibre as a future source of ideas, interoperability, rendering adapters, GIS processing, or plugin capability for Z EARTH. Research-only unless a specific integration is authorized.
---
# GeoLibre research and compatibility skill

- **Verified repository:** https://github.com/opengeos/GeoLibre (not "GeoLibro"). License stated by upstream: MIT; verify exact commit, file license, third-party notices and packaged assets when evaluating a specific component.
- GeoLibre is a standalone GIS application, **not an Agent Skill**. This file gives the Z EARTH Agent a controlled research workflow.
- First map upstream architecture: Tauri/React/TypeScript; MapLibre GL JS, CesiumJS, DuckDB-WASM Spatial, deck.gl, WASM toolchain and their dependencies.
- Study layer catalogs, provider adapters, local-file privacy, project/session persistence, map rendering-engine switching and terrain/time controls.
- Identify _interfaces and patterns_ potentially applicable to Z EARTH's current WPF + WebView2 + CesiumJS stack. Avoid importing entire application or replacing the chosen UI shell without a written ADR and benchmark.
- Candidate future POCs: raster/vector formats, geoprocessing via sandboxed WASM, optional 2D MapLibre pane, multi-provider layer manager.
- For each proposed reuse: record precise upstream repository path, commit SHA/tag, license obligations and transitive dependencies, compatibility with Windows and offline mode, security and attribution.
- No running arbitrary install scripts or adding new network services without reviewed approval. Do not claim that GeoLibre capabilities are already available inside Z EARTH.
- Create an isolated benchmark/spike with simple KML and mixed raster/vector datasets; explicitly validate that no KML metadata/styles are lost when passing through GeoLibre-oriented adapters.

## Deliverable
A short evaluation note with `Use case | upstream path/version | integration approach | interoperability cost | license/security | measured test | recommendation (adopt/defer/reject)` and, if needed, an ADR. No automatic architecture changes.
