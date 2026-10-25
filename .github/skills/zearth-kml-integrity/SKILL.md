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

## Mandatory KML Save/Export and preservation acceptance (2026-10-25)
Read [native and fixture matrix](../../../docs/KML_EXPLORER_ACCEPTANCE_2026-10-25.md). Verify KML path `KmlParser → KmlStyleResolver → PlaceItem → MainViewModel.UpdatePlacesOnGlobe → Cesium loadPlacemarks` and export `KmlSelectionExport → KmlSerializer/KmzService → physical KML/KMZ ZIP reopen`. Check original `Style/StyleMap` semantics and embedded icon byte equality, inherited styles for selected nested nodes, multi-geometry and TimeStamp preservation, high-precision coordinates and SQLite Save As / Open. Reject lossy export and don't claim all Google Earth constructs or native icon display until interactive T3/T4 evidence. Run `KmlExportRoundTripRegressionTests` and legacy Sprint4 fixture tests.
