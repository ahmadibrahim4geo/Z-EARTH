---
name: zearth-kml-integrity
description: Use this skill for importing, editing, rendering, exporting or testing KML/KMZ.
---
# KML fidelity and KMZ safety
- Read `KML_TEST_MATRIX.md` and OGC KML standard before coding.
- Preserve nesting, order, names, geometry, styles, resources, times, ExtendedData and unknown XML.
- Keep a source-of-truth XML model and a separate scene representation; never serialize exclusively from Cesium.
- Validate ZIP entry paths, size limits, XML DTD/XXE, untrusted HTML.
- Write round-trip fixture assertions before editing feature implementation.
- Report P/R/E/W/RT levels honestly; block output when required preserved data would be lost.

## Output
Report changes, exact tests/results, potential data loss, security, licensing, remaining limitations, and the next backlog task.
