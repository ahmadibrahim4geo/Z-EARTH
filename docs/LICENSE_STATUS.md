# Z EARTH source-code licensing status — review required

**As of 26 October 2026:** GitHub does not identify a conventional root `LICENSE` file. The existing [legal/privacy notice](../LEGAL_AND_PRIVACY_NOTICES.md) says “Open-Source Software (GPLv3 / Independent)” and labels product release v1.0.0, while the project README currently describes a v2.2.4 **development preview**. The [third-party license notes](../THIRD_PARTY_LICENSES.md) are not a replacement for an explicit author-granted project-wide source license.

**No new root license has been issued in this documentation PR.** License selection is reserved for the project owner after checking rights in personally authored code and bundled third-party code, imagery, fonts, map tiles, extensions and binary dependencies. The wording “GPLv3 / Independent” by itself is not a complete explicit license grant, and “Independent” is not an SPDX license identifier.

## Proposed decision checklist

1. Inventory all source files, external copied code/assets, CesiumJS/WebView2, GDAL/PROJ, fonts, installer assets and sample data.
2. Identify exact version, upstream license, redistribution requirements, notice preservation, copyleft compatibility and dynamic/linked binaries.
3. Confirm whether the author intends GPL-3.0-only, GPL-3.0-or-later, MIT or another project license; consult counsel when compatibility is uncertain. Do not conflate library licenses with the application's own license.
4. Add an accurate root `LICENSE`, `SPDX-License-Identifier` where applicable, attribution/NOTICE updates and complete third-party details **only after the owner approves**.
5. Update outdated v1.0.0 release/legal labels to distinguish historical documents from present preview source; check legal/privacy claims against actual runtime behavior instead of inheriting assurances blindly.

**Current use caution:** access to a public repository does not by itself prove that every component may be redistributed commercially or that a third-party provider permits offline caching.
