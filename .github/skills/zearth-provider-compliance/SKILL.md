---
name: zearth-provider-compliance
description: Use this skill when adding basemap, imagery, terrain, geocoder, historical or API providers.
---
# Imagery and terrain source contracts
- Read `SOURCES_AND_LICENSES.md` and the provider's current license and Terms.
- Create manifest: stable id, type, URL template, auth mode, copyright attribution, coverage, scale, offline policy, time metadata.
- Never prefetch public OpenStreetMap tiles; don't assume an Esri/Microsoft service is redistribution-free.
- Keep app bootable without a provider. Add fallback and non-blocking error states.
- Never mistake timestamp of imagery service release for image acquisition date.
- Add manual attribution and automated manifests tests.

## Output
Report changes, exact tests/results, potential data loss, security, licensing, remaining limitations, and the next backlog task.
