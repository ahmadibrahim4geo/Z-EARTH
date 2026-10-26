# Z EARTH Desktop GIS — Licensing, legal and privacy notices

**Current source version:** 2.2.4 (development / unsigned portable preview; see `src/ZEarth.App/ZEarth.App.csproj`).  
**Maintainer:** Ahmed Ibrahim (identified as Ahmad Ibrahim in some historic metadata).  
**Project-authored source-code license:** [GNU GPL version 3 only, SPDX `GPL-3.0-only`](LICENSE).  
**Third-party content:** See [THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md) and [LICENSE_STATUS.md](docs/LICENSE_STATUS.md).

The previous legal notice labelled version 1.0.0 a released product and used the ambiguous phrase “GPLv3 / Independent”. That wording has been replaced by a precise license scope. Historical v1.0.0 release notes remain archival; the 2.2.4 source and Actions portable artifacts are **development previews**, not a certified production release.

## 1. License scope and ownership

The [root GPL-3.0-only LICENSE](LICENSE) governs original Z EARTH source files **to the extent the project author has the rights to license those contributions**. Recipients have the permissions and obligations stated in that full license, including source-availability and appropriate notices for covered redistributions. This is **GPLv3 only**, not an automatic grant under later GPL versions.

It **does not relicense** unrelated or externally sourced code, dependencies, binaries, models, data, map services, images, fonts, marks or other copyrighted assets. In particular:

- The independently licensed GIS skills and templates under `.github/` retain their [MIT license](.github/LICENSE) where applicable. Their upstream attributions must be preserved.
- CesiumJS, bundled Cesium engine assets and WebAssembly dependencies are subject to their respective upstream licenses and applicable notices; the [component inventory](THIRD_PARTY_LICENSES.md) documents what remains to verify.
- .NET/WPF, WebView2 and Windows runtimes have their own distribution requirements; an application GPL license does **not** extend OS/software redistribution permissions.
- GDAL/PROJ, NuGet packages, bundled geospatial assets, provider imagery/terrain/tiles and externally obtained KML/KMZ require independent license/terms checks.

**Before publishing a permanent installer or public compiled Release**, validate rights to every redistributed component, complete upstream license/NOTICE inclusion, and confirm GPLv3 compatibility of the proposed combined distribution. Do not assume that this root source license alone resolves those obligations.

## 2. Trademark and compatibility

Z EARTH is an independent GIS development project that works with documented GIS formats and may interoperate with external providers. Product and service names (including Google Earth, ArcGIS, Cesium, WebView2 and others) remain the property of their respective owners. Compatibility goals and public standards do not imply vendor endorsement, guaranteed equivalence or permission to redistribute proprietary software.

The repository does not purport to license Google Earth, Esri, proprietary imagery or paid basemap services. Report any third-party rights or provenance concern privately to the maintainer, and verify suspected copies before making categorical infringement/non-infringement assertions.

## 3. Privacy, connectivity and data handling

The application works with locally selected GIS files, saved scenes and user configuration. **Local-first is a design goal, not a verified universal guarantee**: external basemaps, geocoders, terrain/imagery services, web engines and optional user-enabled features may make internet requests and may share provider-visible network metadata. Provider licensing, terms, attribution, cache restrictions, user consent and privacy behavior require runtime validation.

Do not store or commit credentials, personal GPS histories or private GIS project datasets in public repositories. For support, provide synthetic test inputs and sanitized logs. No blanket guarantee is made that all deployments are completely offline, that all external requests are absent, or that every provider has the same privacy policy.

## 4. Security and test evidence

Archive traversal, excessive decompression, untrusted KML references, WebView2 messaging, third-party scripts and local-file boundaries require negative testing and runtime checks. The repository’s automated build/unit checks (T0–T2) are **not** a substitute for interactive Windows WebView2/GPU checks (T3) or clean-machine deployment acceptance (T4). Issues marked fixed should include the appropriate evidence; otherwise label native acceptance **NOT RUN**.

Security disclosure guidance: [SECURITY.md](SECURITY.md). Build/test limitations: [docs/FRESH_CLONE_VALIDATION.md](docs/FRESH_CLONE_VALIDATION.md).

---

This notice is a project disclosure and inventory aid, not a legal opinion or certification that every external component is compatible or cleared for redistribution.
