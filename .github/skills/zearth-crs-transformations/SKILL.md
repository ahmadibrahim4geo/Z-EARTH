---
name: zearth-crs-transformations
description: Ensure the correct coordinates and spatial reference across every imported layer and viewport.
---

# zearth-crs-transformations

1. Consult docs/03_CRS_COORDINATES_AR.md; use PROJ for transforms.
2. Never infer EPSG from raw numbers; preserve source WKT/CRS and document grid accuracy.
3. KML coordinates lon lat WGS84; never swap latitude accidentally.
4. Test DD/DMS/UTM, datum, meters/feet, antimeridian, projected Egypt points and CRS missing.
5. Report transform operation, accuracy and error messages.

**Evidence gate:** report tests executed, exact failures and files changed. Do not claim complete from mock screenshots.
