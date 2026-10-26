# Contributing to Z EARTH / المساهمة

Thank you for contributing to this open geospatial development project.

1. Read [project overview](docs/REPOSITORY_ONBOARDING.md), [README](README.md), [Arabic README](README.ar.md), [AGENTS.md](AGENTS.md) and the [license status](docs/LICENSE_STATUS.md).
2. Open a focused issue with the user-visible workflow, observed failure, target Windows/WebView2/Cesium version, sample format and expected output.
3. Make one focused branch per fix. Attach a reproducible test, then open a PR with a summary, exact commands, native evidence (if available) and known limitations.
4. Preserve existing features, user settings and KML/KMZ styles/folders/assets. Test round-trip import/export/reopen rather than only parser output.
5. Distinguish checks: static/syntax T0–T1; automated T2; real Windows interactive T3; clean-machine T4. **Never present NOT RUN native tests as passed.**
6. Do not commit paid API keys, local project data, private GPS positions, commercial GIS SDK binaries, or nonredistributable cartographic assets.
7. Native bugs should use a **sanitized** synthetic KML/KMZ file and include screenshot/logs with private paths removed.

**بالعربية:** افتح بلاغًا يصف الخطأ وخطوات إعادة حدوثه، وأرفق دليل الاختبار الفعلي. لا تعتبر نجاح Unit Tests إثباتًا لعمل واجهة Windows أو محرك Cesium الحقيقي. لا تضف ملفات ترخيصها غير واضح أو بيانات مواقع خاصة.
