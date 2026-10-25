---
name: zearth-rea-parity
description: Use the existing local morluto/rea Reverse Engineer Anything tool for evidence-based, authorized Google Earth Pro desktop behavior comparison without copying proprietary assets or guessing features.
license: MIT
---

# REA behavior research for Z EARTH

**Tool, not target:** [REA (morluto/rea)](https://github.com/morluto/rea) is an MIT-licensed Reverse Engineer Anything analysis MCP/CLI. It is **not** a Google Earth Pro source repository. The user's local checkout has previously been recorded at `D:\\My Software\\Zearth\\rea`; `/rea/` is excluded from Z EARTH Git. Do not download a duplicate, overwrite it, or automatically reinstall packages.

## Setup check on user's authorized Windows machine

1. Inspect the existing local folder with read-only file listing, `git -C "D:\\My Software\\Zearth\\rea" status --short`, `git rev-parse HEAD` and the installation's own README. The existing checkout may have uncommitted work or customized MCP settings. **Never reset its Git state.**
2. Check whether REA is already connected in the user's chosen agent/IDE. Use `rea --help` (if CLI present) or installed MCP tool catalog. **Do not run `npx rea-agents setup` automatically**: the user has an existing integration that may already be configured.
3. REA's current published docs list Node.js **22.x (>=22.23.4), 24.x (>=24.22.1) or 26.12.1+**. Verify local version and required optional binary backend if native analysis is actually needed. No paid decompiler or cloud model is a default dependency.
4. Only inspect software the user is authorized to analyze, including any applicable license conditions. Prefer **documented UI workflows and read-only runtime observation**. Avoid defeating DRM, license checks, authentication or services; follow [REA's Disclaimer](https://github.com/morluto/rea/blob/main/DISCLAIMER.md).

## Evidence-driven Google Earth Pro → independent Z EARTH comparison

For each **one feature at a time** (My Places, Temporary Places, save/reorder startup scene, KML/KMZ, geocoding, basemaps, terrain, Ruler, profile, compass, 2D/3D), do:

- Specify **observable user behavior** and the state transition, source and target versions, file type, data fixture and expected output.
- Read the original application's user docs first. Only then run a small **authorized** REA observation of user-visible behavior where permitted. For installed app directory detection, REA documents `inspect_desktop_application` (bounded read-only headers/metadata, no binary execution). For .NET managed applications it documents `inspect_managed_artifact` and `inspect_managed_members`; use only when applicable. These capabilities do not imply Google Earth Pro is .NET.
- Record source evidence, screenshots if allowed, test steps, observed vs inferred behavior, missing coverage, errors, and data provenance. Do not publish proprietary code/tiles, network tokens, personal My Places or protected service traffic.
- Translate observations into independent **acceptance tests** for WPF + CesiumJS + GDAL/PROJ. Use the project's `zearth-globe-bridge`, `gis-desktop-application-development`, `geospatial-web-mapping`, `coordinate-reference-systems` and `kml-kmz-engineering` skills. Never paste decompiled/copyrighted application implementation into Z EARTH.
- Require separate real **T3 Windows/WebView2 visual behavior** and **T4 deployment** evidence; REA's read-only metadata, scripted fake engines or static tests do not prove correct terrain or UX.

## Handoff artefact per investigated feature

Write a small project-safe report to `docs/rea-evidence/<feature-slug>.md` (no raw proprietary data): expected steps; inspected program/build and REA revision; actual observed steps; UI/Places state before and after; data safety/rights; independent design; test names/results; explicitly unknown aspects; screenshots **only if authorized and redacted**. Then implement in feature-specific PR with negative cases.

Follow [the parity acceptance matrix](../../../docs/GOOGLE_EARTH_PRO_PARITY.md) and [the free-first policy](../../references/free-first-policy.md). The Z EARTH application must remain useful without this optional analysis tool.
