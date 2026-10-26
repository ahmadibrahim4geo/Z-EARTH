# Z EARTH — license status and release compliance

**Source license selected:** [GNU General Public License version 3 only (`GPL-3.0-only`)](../LICENSE) for the independently authored project code to the extent of the maintainer's ownership/authority. This is the full, unmodified GPLv3 license text from the [SPDX reference](https://github.com/spdx/license-list-data/blob/main/text/GPL-3.0-only.txt). The new root license replaces the ambiguous historic wording “GPLv3 / Independent” for *project-authored code*.

**Current version:** 2.2.4 development source / unsigned portable preview. v1.0.0 release notes are historical, not current production certification.

## What is covered

- **Project author's original code:** GPL-3.0-only (not “or later”).
- **Bundled third-party code, CesiumJS assets, WebAssembly, libraries, SDKs, geospatial datasets/tiles/fonts:** their respective upstream licenses/usage terms remain effective. Z EARTH cannot relicense those materials just by adding a root LICENSE.
- **Copied GIS Development Skills under `.github/`:** previously recorded [MIT license](../.github/LICENSE) and original attributions apply independently.

See [third-party inventory](../THIRD_PARTY_LICENSES.md) and [legal/privacy notices](../LEGAL_AND_PRIVACY_NOTICES.md).

## Verification completed and outstanding

**Completed in the source audit (26 October 2026):** GPL-3.0-only legal text retrieved verbatim; inspected the root legal notice, `.github/LICENSE`, README, .NET package versions and the Git tree. The repository contains vendored CesiumJS/worker assets and independent license scopes that need preservation.

**Not verified / outstanding before declaring a compiled release redistribution-compliant:** a complete executable dependency and asset license/NOTICE manifest; exact vendored Cesium version/license copies; WebAssembly origins; third-party fonts/images; Microsoft runtime terms and linkage; build-artifact corresponding-source delivery; provider tile caching/attribution; ownership/permissions for any non-original code. CI and static checks cannot replace a legal review.

**Next action:** preserve full upstream licenses and attribution/NOTICE for all shipped binaries/assets, complete a versioned SBOM/build manifest, and review actual Windows published artifact rights before promoting a preview to an official production release.

This checklist records due diligence, not a legal opinion or guarantee.
