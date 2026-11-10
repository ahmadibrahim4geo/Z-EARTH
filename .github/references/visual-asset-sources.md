# Visual asset sources and optional related skills

Reviewed 2026-10-27. This is an extensible starting point for [Visual Asset Discovery & Integration](../skills/visual-asset-discovery-integration/SKILL.md), not a closed provider allowlist, automatic installation, unlimited service promise or blanket reuse grant. Use actual host search/retrieval tools; keep private search inputs private.

## Source routes

| Need | Start here | Verification and integration |
| --- | --- | --- |
| Official company/software/service logo | That organization's current brand, press or media resources | Confirm correct entity/variant, brand conditions and downloadable original; preserve clear space, proportions, colors and alpha |
| Open-media discovery across sources | [Openverse](https://openverse.org/) · [consumer API](https://api.openverse.org/v1/) · [API documentation](https://docs.openverse.org/api/) | Original-source license confirmation remains necessary; respect current authentication, quotas and Retry-After; do not hardcode a free/unlimited claim |
| Photographs, diagrams, historic imagery | [Wikimedia Commons](https://commons.wikimedia.org/) · [reuse guide](https://commons.wikimedia.org/wiki/Commons:Reusing_content_outside_Wikimedia) · [MediaWiki imageinfo](https://www.mediawiki.org/wiki/API:Imageinfo) | Confirm the file page, author, license and attribution; imageinfo can expose source/thumbnail URLs, dimensions, MIME and extended metadata; treat HTML metadata as untrusted |
| Brand SVG catalog | [Simple Icons](https://github.com/simple-icons/simple-icons) · [disclaimer](https://github.com/simple-icons/simple-icons/blob/develop/DISCLAIMER.md) | Catalog marks may be simplified; prefer the official full logo when needed. Package CC0 does not settle individual brand rights; inspect per-icon license/guidelines metadata |
| Actual terrain, bathymetry or raster preview | The selected provider/data and project's renderer; [Terrain specialist](../skills/terrain-hydrology-lidar/SKILL.md) | Render/capture the real model when the brief requires a factual preview. A generic mountain, a provider logo and a real elevation preview have different meanings |
| Any other project subject | Subject-specific official galleries, museums, scientific institutions, repositories and wider web/image search | Expand queries/synonyms/languages; review each original rather than assuming an institution's entire collection is freely reusable |

Searching, viewing and downloading are separate capabilities. Do not copy search-engine thumbnails into a product as if they were the reviewed original. Bundle selected local files and required credits when offline operation is part of the goal.

## Related skills and host capabilities

| Reference | Useful role | Limits / adoption |
| --- | --- | --- |
| Host-provided `imagegen` skill and actual image-generation tool | New illustrations, cutouts and visual variants | Discover at runtime. A skill name does not guarantee its tool exists. Prefer the host's supported generation path; do not silently switch to a paid/key-based API. Never generate an existing official logo or factual model preview |
| Host image/web search and source connectors | Find images, original source pages, official downloads and licensing evidence | Retrieve and inspect the actual file. A response image gallery alone does not integrate a reusable asset into code |
| [Anthropic Canvas Design](https://github.com/anthropics/skills/tree/main/skills/canvas-design) · [skill](https://github.com/anthropics/skills/blob/main/skills/canvas-design/SKILL.md) · [license](https://github.com/anthropics/skills/blob/main/skills/canvas-design/LICENSE.txt) | Optional static visual composition/posters as PNG/PDF | Apache-2.0 skill license inspected; review font/asset licenses separately. It creates compositions, not an internet-image retrieval engine. Reference only: not copied, installed or tested here |
| [obra/muse-skills](https://github.com/obra/muse-skills) | Candidate image-search integration to investigate | The inspected `image-search` entry points to a platform-specific external runtime rather than a self-contained public skill folder. Exclude it as an automatic dependency; no portable implementation/license was established. Not installed or executed |
| Existing [UI/UX specialist](../skills/gis-ui-ux-design-testing/SKILL.md) and target Desktop/Web/Mobile skills | Target-size readability, accessibility, resource bindings and packaging | Keep owners separate; source acquisition does not prove target UI loading or packaged offline behavior |

No unreviewed marketplace install command or downloaded script is needed for the core workflow. Use existing permitted tools first, inspect any optional skill/version/license before adoption, and record runtime evidence and remaining gaps.
