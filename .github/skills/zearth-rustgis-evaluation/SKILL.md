---
name: zearth-rustgis-evaluation
description: Consult when exploring RustGIS or Rust-based native geospatial algorithms as a future performance extension for Z EARTH. Research-only unless an isolated integration is authorized.
---
# RustGIS evaluation and acceleration skill

- **Verified repository:** https://github.com/opengeos/RustGIS (sometimes referred to as "GIS Rust"). Upstream license: Apache-2.0; check exact relevant crate and transitive dependencies before use.
- RustGIS is a **standalone pure-Rust GIS application**, not a drop-in skill or a compatible replacement for WPF/Cesium. This SKILL.md defines how the Agent assesses it.
- Review the modular crates `rustgis-geom`, `rustgis-data`, `rustgis-analysis`, `rustgis-tiles` and CLI/MCP interfaces. Verify actual exported APIs for selected commit, not only README descriptions.
- Candidate future POCs: indexing/large-file parsing, geometry validation and spatial operations, efficient tile caching, GeoPackage/GeoParquet I/O, native analysis modules, background processing, WASM plugins.
- Check integration routes independently: (1) Rust-based command-line worker using JSON/GeoPackage, (2) stable C ABI/FFI wrapper, (3) Rust/WASM component in trusted WebView. Do not assume upstream provides ready-made C# bindings; ownership/cancellation, exceptions, memory and deployment must be addressed.
- Benchmark against existing .NET/GDAL implementation: time, peak RAM, throughput on reproducible 10k/100k/1M geometry fixtures and correctness checks. Do not force Rust adoption when the current C# pipeline is sufficient.
- Preserve original KML/KMZ and the Z EARTH project schema independently of imported modules. No silent data-model migrations or automatic license/SDK changes.
- Verify security, license attribution, version pinning, install size, Windows x64 packaging, offline behavior, test coverage and maintenance state.

## Deliverable
One feasibility report: `benchmark | source version | API boundary | test data | measured results | file fidelity | packaging/licensing | ADR`. Default status = future candidate; no migration without evidence.
