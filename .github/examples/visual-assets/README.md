# Images, thumbnails and official logos

Use [Visual Asset Discovery & Integration](../../skills/visual-asset-discovery-integration/SKILL.md) through the [main router](../../skills/gis-development/SKILL.md). Follow the [workflow](../../references/visual-asset-workflow.md) and [source/skill guide](../../references/visual-asset-sources.md). This example is platform-neutral; it does not modify Z-EARTH or any other application.

## Copyable requests

**English:** Find and integrate a suitable image for each option in this interface. Inspect the provider's own resources and broaden internet/image searches as needed. Use official logos for brand identities, actual data previews when required, and explicitly identified illustrations otherwise. Deliver local assets, the correct small-size/density variants, source credits and target UI loading evidence.

**العربية:** ابحث عن صورة مناسبة لكل اختيار في الواجهة وادمجها داخل المشروع. افحص صور المصادر الرسمية ووسّع البحث على الإنترنت حسب الحاجة. استخدم الشعارات الرسمية للجهات، ومعاينات البيانات الفعلية عند طلبها، وميّز الصور التوضيحية. جهّز الملفات محليًا بالأحجام المناسبة وسجّل المصادر والتراخيص واختبر ظهور الصور داخل البرنامج.

## Acceptance scenarios

| Request | Expected route and output | Failure to catch |
| --- | --- | --- |
| Small image for each terrain model | Actual model/data capture if factual; otherwise clearly illustrative local thumbnail | Repeated generic mountain icons presented as distinct real model previews |
| Logos for service/source cards | Official verified brand assets, contain/padding, credits and correct local binding | Invented/recolored mark, cropped lettering, wrong organization |
| Illustrations for a non-GIS learning app | Topic-appropriate web search or available generation/design tools, explicit representation type | Hardcoded terrain-only source selection |
| 64-pixel thumbnail on a 2× display | Suitable original and 64/128-pixel variants; real size review | Distortion, unreadable detail or unsupported upscaling |
| Offline packaged project | Shipped local resources and credit text; real load/readback test | A hotlinked URL that only worked in a development browser |
| No search tool or uncertain license | Honest missing capability, alternative reviewed source or blocked asset | Fabricated download, success flag or automatic paid provider |

## Test evidence boundary

`python -m unittest discover -s tests -p 'test_visual_assets.py' -v` tests deterministic local raster preparation, integrity, metadata rejection, safe new outputs and bilingual routing. Synthetic fixtures are created in temporary directories; no third-party images or supplied screenshots are copied into the repository.

The tests are T0/T1 evidence. Live provider retrieval, image-generation calls, scientific engine capture, target UI and packaged offline acceptance require their actual host/tools and must be recorded separately as T2–T4, or NOT RUN. The raster helper does not search or download images.

