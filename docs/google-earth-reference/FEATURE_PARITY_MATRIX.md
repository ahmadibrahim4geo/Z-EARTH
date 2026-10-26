# Google Earth / ZEarth independent feature coverage matrix

**Date:** 2026-10-26. **Source of truth:** connected Drive directory metadata plus inspected ZEarth GitHub source. "Source-implemented" means code paths exist; NOT equivalent to runtime working. Never use Google binaries or shaders in ZEarth. Feature names from the installation do not establish live reference behavior. User-reported cloud behavior is marked separately.

| Feature | Reference evidence | ZEarth evidence | Classification | Next acceptance action |
|---|---|---|---|---|
| 3D globe and navigation | Resources suggest navigation | Source-implemented Cesium viewer | SHARED | T3 native GPU camera and gesture tests |
| 2D map | Not assessed from this installation | Implemented 2D mode | ZE-EXTRA | Preserve 2D projection |
| Columbus/2.5D mode | Not assessed | Source handler in JS | ZE-EXTRA | Expose only after tested UX |
| Compass and north modes | Not assessed | Multiple source compass configurations | ZE-EXTRA | Preserve choices and test pole edge cases |
| Adaptive meridian intervals | Not assessed | Independent axis intervals | ZE-EXTRA | Prevent stale lines when disabled |
| Major geographic circles | Not assessed | Named parallels, poles, Greenwich, dateline | ZE-EXTRA | Test behind-globe culling |
| UTM zone overlay | Not assessed | Source implementation | ZE-EXTRA | Check UTM exceptions and grid off |
| Atmosphere / fog | Shader resources suggest category | setAtmosphere source implemented | SHARED | Verify visible on/off readback |
| Sunlight / UTC time | Lighting shader resources | setSunlight source implemented | SHARED | Verify date/solar rendering |
| Illustrative 3D clouds | User-reported reference behavior; not validated | NEW CloudCollection offline source | NEW-ZE | Native GPU screenshot, expose as illustration |
| Live cloud observations | Not verified from reference files | No live weather provider wired | GAP/OPTIONAL | Only licensed free meteorological imagery with timestamps |
| Stars and night sky | stars shader file | Cesium sky background needs review | UNVERIFIED | Visual tests only |
| 3D trees/vegetation | SpeedTree shader resources | No tree pipeline established | GAP/OPTIONAL | Use licensed open 3D assets only |
| Flight simulator | flightsim resource tree | No confirmed feature | GAP/OPTIONAL | Defer pending stable core and acceptance specs |
| KML 2.2 import/export | KML extension/icons/resources | Parser/serializer present | SHARED | Semantic round-trip fixtures |
| KMZ archive resources | KMZ icon/Places resources | KmzService includes safe extractor | SHARED | Round-trip embedded images/icons/models |
| gx:Tour | tourcontrols.rcc | Preservation support uncertain; playback not proven | GAP/REVIEW | Study OGC/public gx semantics, prototype independently |
| NetworkLink refresh | Not confirmed by filenames | Preserved XML fragments in parser | GAP/REVIEW | Security-aware URL refresh and update semantics |
| Places / Temporary | default_myplaces.rcc | Source hierarchy and temp sessions | SHARED | Close/reopen native integration tests |
| Time travel imagery | tmcontrols.rcc | Esri Wayback release timeline | DIFFERENT | Do not mislabel release as capture date |
| Search locations | search.rcc and Qt5Network | Local and Photon provider documented | SHARED | Offline/Arabic/English and rate-limit tests |
| Street View | Not verified in reference folder | No licensed Street View equivalence | EXCLUDE-PAID | Do not bypass proprietary services |
| Google aerial imagery | Proprietary service not in folder | Free-first imagery catalogs | EXCLUDE-PAID | Provider license audit; no tile copying |
| Vector layers and GDAL | gdal204.dll, proj.dll, GDAL data | GDAL/PROJ and OGR integrated | SHARED | Shapefile/GeoJSON/GPKG end-to-end |
| Geodetic CRS | proj.dll and GDAL CRS files | EPSG/UTM and CRS UI | SHARED | Ellipsoid and axis order checks |
| Elevation profiles | Terrain resource | Profile UI; DEM source requirements | SHARED/NEEDS-VALIDATION | Never synthesize terrain Z |
| 3D models | 3D renderer resources | Model import path and place anchor | GAP/REVIEW | Verify glTF and Collada model rendering |
| Measurement and drawing | Not verified by filenames | Measurements, draw modes, snapping | ZE-EXTRA | Real result verification |
| Arabic localization | ar.qm and 41 other language files | Arabic/English UI | SHARED | Native bilingual string checks |
| Offline startup and data | Local native libraries | Cesium offline ellipsoid/vector mode | ZE-EXTRA | Start fully offline and reopen |

## Free-first candidate for observed cloud cover (not procedural 3D clouds)

The new ZEarth **Clouds** feature is offline visual cumulus, not an observation layer. A *separate*, optional weather overlay could use [NASA GIBS public WMS/WMTS](https://nasa-gibs.github.io/gibs-api-docs/) and specific cloud-fraction/cloud imagery datasets documented by NASA. GIBS supports time-varying WMS, but the selected layer name, availability, timestamps, attribution, rights, cloud mask interpretation, geographic projection and tile requests must be confirmed from provider capabilities before integration. Do **not** label imagery as real-time or as volumetric 3D meteorology.

Acceptance for optional GIBS provider: choose a documented valid layer/date → fetch authoritative capabilities and tile → display timestamp, coverage and attribution → support offline/timeouts transparently → remove layer and clear cache per terms → avoid API keys or paid Google services. This is **candidate research**, not implemented or verified.

## Action order
**P0:** cloud native render/build, grid visibility and poles, KML/KMZ data round-trip; **P1:** sunlight, camera and offline search; **P2:** weather cloud providers, models, tours; **P3:** flight simulator and vegetation. Preserve ZEarth-only features. Exclude Google-owned imagery, Street View and other restricted content, unless legitimately licensed alternatives exist.

## Evidence statuses
- **DIRECTORY OBSERVED:** file/folder metadata listed via connected Google Drive; not executable proof.
- **SOURCE INSPECTED:** Github text contains a corresponding function, but user interaction not verified.
- **MOCK TESTED:** algorithm verified under controlled fake Cesium API, not actual WebGL/GPU.
- **NATIVE VERIFIED:** requires documented Windows runtime trace/screenshots and a reproducible test, currently none for these new features.
