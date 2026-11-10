# Z EARTH — Prompt تنفيذ مراجعة التضاريس وBasemap وشبكة الإحداثيات

**Request ID:** `ZEARTH-TBG-20261028`  
**Plan revision:** `r1`  
**Status:** `IMPLEMENTED T0–T2 — NATIVE VISUAL ACCEPTANCE PARTIAL 2026-10-28`  
**Target:** `D:\My Software\Z EARTH\canonical-main`

## الدور والهدف

أنت مهندس GIS Desktop مسؤول عن إصلاح مسار التضاريس والخرائط الأساسية وشبكة
الإحداثيات في **Z EARTH**، وهو تطبيق Windows WPF/.NET مع CesiumJS داخل
WebView2. المطلوب تنفيذ مستقل وقانوني مستلهم من سلوك Google Earth Pro المفيد،
دون نسخ كود أو أصول Google ودون اختلاق قيم ارتفاع.

النتيجة المطلوبة للمستخدم:

1. مصادر التضاريس المجانية تظهر كخيارات مستقلة وصادقة وتعمل بالطريقة المناسبة
   لنوعها، مع فصل GEBCO وCopernicus DEM عن Cesium ion.
2. زر `Basemap > Browse All` يفتح نفس صفحة `Basemap & Display` داخل نافذة
   Options، بنفس المكونات والتصميم، بدل نافذة أخرى مختلفة في الشكل والسلوك.
3. `Esri World Imagery (Hybrid Labels)` هو الافتراضي القانوني الموحد عند أول
   تشغيل أو عند غياب/فساد قيمة الإعداد، مع بقاء اختيار المستخدم المحفوظ محترمًا.
4. شبكة الإحداثيات وخطوطها وتسمياتها مستقرة وواضحة ومشابهة في السلوك العملي
   لـGoogle Earth، من دون ظهور عشوائي أو اختفاء أجزاء أثناء الحركة والتكبير.
5. قيم المنسوب والـElevation Profile تأتي من مصدر ارتفاع رقمي نشط ومعلن،
   والمبالغة الرأسية تغيّر العرض فقط ولا تغيّر قيم DEM الأصلية.

## التعليمات الإلزامية قبل أي تعديل

1. اقرأ `AGENTS.md` ثم:
   - `.github/skills/prompt-intent-orchestrator/SKILL.md`
   - `.github/skills/gis-development/SKILL.md`
   - `.github/skills/gis-desktop-application-development/SKILL.md`
   - `.github/skills/geospatial-web-mapping/SKILL.md`
   - `.github/skills/terrain-hydrology-lidar/SKILL.md`
   - `.github/skills/coordinate-reference-systems/SKILL.md`
   - `.github/skills/gis-ui-command-audit/SKILL.md`
   - `.github/skills/gis-ui-ux-design-testing/SKILL.md`
   - `.github/skills/gis-desktop-e2e-testing/SKILL.md`
   - `.github/skills/gis-security-guardian/SKILL.md`
   - `.github/skills/completion-evidence/SKILL.md`
   - `.github/skills/agent-development-journal/SKILL.md`
2. اقرأ العقود والمراجع الفعلية:
   - `docs/GOOGLE_EARTH_PRO_PARITY.md`
   - `docs/TERRAIN_GRID_ACCEPTANCE.md`
   - `docs/ZE_UI_FUNCTIONAL_AUDIT_2026-10-25.md`
   - `docs/google-earth-reference/README.md`
   - `docs/google-earth-reference/FEATURE_PARITY_MATRIX.md`
   - `Development Reports/2026-10-28_GE_Functional_Spec_Backlog.md`
   - التقرير العربي الأصلي
     `D:\My Software\Z EARTH\Com_google earth\Google_Earth_Pro_to_Z_EARTH_Complete_Functional_Spec_AR.md`
3. اربط العمل ببنود التقرير:
   `VIEW-006`, `VIEW-015`, `TOOLS-007`, `TOOLS-013`, `OPT3D-001`,
   `OPT3D-002`, `OPT3D-005`, `OPT3D-006`, `LAYER-001`, `LAYER-006`,
   `LAYER-007`, `LAYER-012`, و`GRID-001` إلى `GRID-010`.
4. افحص سلسلة كل وظيفة قبل تعديلها:
   `XAML → MainViewModel → GlobeBridge → cesium-engine.js → Cesium scene → WPF state`.
5. حافظ على تعديلات المستخدم الحالية، ولا تنشئ Preview أو Release ولا تغيّر
   مزودًا أو محركًا بصمت.

## حقائق يجب تثبيتها وعدم خلطها

### GEBCO 2026

- GEBCO 2026 شبكة عالمية land/ocean بدقة **15 arc-second** وقيم بالمتر، وليست
  نموذج 20 متر وليست خدمة Cesium TerrainServer جاهزة.
- البيانات مجانية وفق شروط GEBCO مع attribution. اعتمد التنفيذ الحالي adapter
  متصلًا يقرأ الشبكة الرقمية الرسمية من CEDA OPeNDAP عبر GDAL ثم يحولها إلى
  height tiles داخل العملية؛ ولا يستخدم WMS أو يدّعي أن GEBCO TerrainServer.
- لا تطلب Cesium ion token لمسار GEBCO المحلي/المستقل.
- أبقِ تحذير أنها ليست للملاحة البحرية وأن الدقة الشبكية لا تساوي دقة القياسات
  الأصلية.

### Copernicus DEM GLO-30

- أضف `Copernicus DEM GLO-30` كمصدر مستقل واضح، وليس مجرد وصف داخل
  `Cesium World Terrain`.
- هو DSM عالمي sampling يقارب 30 متر، ويشمل المباني والنباتات، وليس DTM bare
  earth.
- خدمته الرسمية مجانية لكنها تتطلب حساب Copernicus Data Space/CCM ورمز OAuth
  قصير العمر. لا تخزن credentials في الإعدادات أو السجل.
- المسار المحلي أو المحوّل لا يحتاج ion token. استخدام نسخة مستضافة في ion هو
  مسار اختياري مختلف يُسمّى بوضوح.

### Cesium ion

- `Cesium World Terrain` و`Cesium World Bathymetry` مزودان منفصلان يحتاجان
  token وصلاحية asset وخطة/حصة استخدام مناسبة.
- لا تسمِّ `Cesium World Bathymetry / GEBCO 2023` باسم `GEBCO 2026`، ولا تجعل
  نجاح ion دليلًا على أن مسار GEBCO المستقل يعمل.
- لا تحفظ token في `settings.json` أو logs؛ أقل صلاحية ممكنة ويفضل session-only.

### مصادر مجانية مرشحة

أنشئ provider catalog صريحًا، وقيّم على الأقل:

- AWS Open Data Terrain Tiles / Terrarium: مصدر رقمي tiled موجود حاليًا في
  المشروع، لا يحتاج key، لكن يجب فحص attribution والمصدر الرأسي والتغطية
  ونجاح البلاطات الفعلية.
- NASA/USGS SRTMGL1: قرابة 30 متر في نطاق تغطيته، مع بيان الحدود والـEGM96
  والفراغات/NoData، وليس Global كاملًا عند القطبين.
- JAXA AW3D30: DSM قرابة 30 متر، متاح مباشرة عبر JAXA Earth API بلا تسجيل أو
  API key، مع attribution وشروط البيانات؛ لا يوصف كـbare-earth DTM.
- Copernicus GLO-90 كبديل عالمي أخف عند الحاجة.

لا تضف أي مصدر للقائمة لمجرد أن تنزيله مجاني. كل صف يجب أن يحدد:
`provider id`, النوع `dataset/service`, sampling, coverage, horizontal CRS,
vertical reference, units, NoData, authentication, license/attribution,
online/offline mode, readiness, وآلية التحويل إلى Cesium.

## المهمة 1 — تدقيق وإعادة تصميم Terrain Provider Architecture

1. افحص الوضع الحالي في:
   - `src/ZEarth.App/Services/SettingsService.cs`
   - `src/ZEarth.Domain/Terrain/TerrainProviderConfig.cs`
   - `src/ZEarth.App/Views/OptionsDialog.xaml(.cs)`
   - `src/ZEarth.Globe/GlobeBridge.cs`
   - `src/ZEarth.Globe/Assets/cesium-engine.js`
2. افصل ثلاثة مفاهيم في النموذج والواجهة:
   - **Ready streaming terrain provider** يمكن تفعيله الآن.
   - **Downloadable DEM dataset** يحتاج download/prepare/import.
   - **Local/custom prepared terrain** له مسار أو URL صالح وprobe حقيقي.
3. لا تفعّل `gebco2026` أو `copernicusGlo30` قبل نجاح probe رقمي. التنفيذ
   يستخدم endpoint رسميًا صالحًا لكل مصدر: CEDA OPeNDAP لـGEBCO وCDSE Process
   API لـCopernicus، مع fallback صريح إلى ellipsoid عند الفشل.
4. قارن وسجّل قرارًا معماريًا بين:
   - adapter محلي يقرأ GeoTIFF/NetCDF ويقدم height tiles بصورة آمنة؛
   - quantized-mesh محلي/loopback موثق؛
   - TerrainServer HTTPS يختاره المستخدم؛
   - ion كخيار منفصل فقط.
5. لا تنزّل ملف GEBCO العالمي الضخم تلقائيًا، ولا تشغّل خادمًا دائمًا، ولا
   تستخدم localhost/remote URL من دون allowlist/SSRF checks وإيقاف نظيف.
6. كل تفعيل يجب أن ينفذ readiness probe عدديًا، يثبت وجود قيم غير وهمية، ثم
   يرسل `terrain.changed`. عند الفشل ارجع إلى ellipsoid مع السبب الحقيقي ومن
   دون badge أخضر كاذب.

## المهمة 2 — المنسوب والمبالغة الرأسية

1. اجعل status bar وElevation Profile يذكران المصدر النشط، الوحدة، والمرجع
   الرأسي عندما يكون معروفًا.
2. لا تعرض `0` أو ellipsoid height كأنه منسوب تضاريس.
3. عينات القطاع الطولي تأتي من الـactive numeric terrain provider فقط، مع
   timeout/NoData/error states.
4. vertical exaggeration يؤثر على `scene.verticalExaggeration` أو الآلية
   المعتمدة في الإصدار المستخدم فقط؛ القيمة العلمية المعروضة تظل الأصلية.
5. اختبر 1× و1.5× و3× على موقع معروف التضرس، وقارن عينة ارتفاع ثابتة قبل/بعد؛
   يجب أن يتغير الشكل ولا تتغير قيمة المصدر.

## المهمة 3 — توحيد واجهة Basemap

1. غيّر `Basemap > Browse All` و`View > Base Map` لفتح `OptionsDialog` مباشرة
   على `OptionsDialogTab.BasemapDisplay`.
2. لا تستخدم `BasemapGalleryDialog` كنافذة اختيار عامة ثانية. انقل إلى صفحة
   Options أي ميزة لازمة منها: search، source/category filters، thumbnails،
   active/default indicator، attribution، availability/error state.
3. إذا بقيت أداة Swipe، اجعلها أمرًا متقدمًا منفصلًا باسم واضح، أو ادمجها داخل
   نفس صفحة Basemap؛ لا تجعلها سببًا لبقاء معرضين مختلفين.
4. استخدم نفس design tokens لنافذة Options: dark gray، borders، typography،
   spacing، hover/focus/selected/disabled/loading/error، مع English default
   وArabic RTL كامل وDPI 100/150/200%.
5. اختيار البطاقة يعمل كـpreview قابل للإلغاء؛ `Cancel` يعيد المصدر الأصلي،
   و`Save Settings` يثبت default ويحدّث Ribbon/WPF/Cesium مرة واحدة.

## المهمة 4 — تثبيت Esri Hybrid كـdefault

1. وحّد القيمة canonical إلى `esri-hybrid` في:
   - AppSettings fresh defaults؛
   - MainViewModel initial state؛
   - Options selection؛
   - Cesium/JS fallback IDs؛
   - الاختبارات وdefault scene عندما لا توجد قيمة صالحة.
2. عالج التناقض الحالي: `AppSettingsModel.BasemapId` يبدأ بـ`offline-vector`
   بينما `MainViewModel._selectedBasemapId` يبدأ بـ`esri-hybrid`.
3. قواعد startup:
   - fresh/no settings/blank/unknown id → `esri-hybrid` مع hybrid labels ON؛
   - valid user-saved default → احترمه؛
   - provider/network failure → fallback مرئي إلى `offline-vector` للجلسة فقط،
     ولا تعِد كتابة تفضيل المستخدم بصمت؛
   - عند عودة الخدمة يمكن للمستخدم retry.
4. لا تعتبر إنشاء imagery provider نجاحًا؛ اختبر tile response/render وattribution.

## المهمة 5 — شبكة الإحداثيات والـlabels

1. استخدم Google Earth كمرجع سلوكي فقط: خطوط واضحة، labels مقروءة ومتسقة مع
   المقياس، لا نسخ للأصول أو التصميم الحرفي.
2. شخّص الاختفاء المتقطع في implementation الحالي، خصوصًا:
   - rebuild أثناء camera changed/move end؛
   - view rectangle عند الميل والقطبين وanti-meridian؛
   - label thinning/edge padding/collision boxes؛
   - distance display conditions وdepth/horizon clipping؛
   - terrain clamping و2D/3D morph؛
   - اختلاف canvas size وDPI.
3. المطلوب ليس إظهار كل label فوق بعضه؛ المطلوب LOD deterministic. عند نفس
   الكاميرا والحجم تظهر نفس الخطوط والتسميات، ولا تختفي نصف الشبكة عشوائيًا.
4. أبقِ Equator، Prime Meridian، Date Line، Tropics، Polar Circles، poles و0/0
   بأولوية أعلى، مع منع التكرار وعدم الظهور على ظهر الكرة.
5. DD/DM/DMS/whole-degree والاتجاهات N/S/E/W تعمل عند 0° و±180° والجنوب/الغرب،
   وUTM zones تراعي Norway/Svalbard exceptions.
6. اختبر toggle الكلي، latitude/longitude المستقلين، labels، UTM، الألوان،
   intervals، الحفظ وإعادة التشغيل. يجب ألا تبقى entities أو labels بعد OFF.

## المهمة 6 — الاختبارات ودليل الإنجاز

أضف regression tests للعقود التالية قبل الإصلاح ثم اجعلها تمر:

1. Terrain source identity: GEBCO/Copernicus المحليان لا يطلبان ion token؛
   World Terrain/Bathymetry يطلبانهما فقط.
2. Provider readiness/fallback والرسائل وعدم وجود fake elevations.
3. Basemap command trace من XAML إلى Options Basemap tab.
4. `esri-hybrid` fresh default واتساق settings/VM/JS، مع احترام saved default
   وoffline session fallback.
5. Grid formatter/geometry/LOD/collision/horizon/anti-meridian/persistence.
6. Elevation sample provenance وثبات القيمة مع vertical exaggeration.

ثم نفّذ وسجّل:

- T0: source/command/provider/license/CRS review.
- T1: JS syntax، Node tests، .NET build، unit/contract tests.
- T2: provider fixture/real tile or prepared local sample readback مع قيم متوقعة.
- T3: Windows WebView2/GPU: تشغيل فعلي، تضاريس، Basemap، Grid pan/zoom/tilt،
  labels، restart persistence، offline/error cases، screenshots/logs.
- T4: Preview وحيد من `scripts/build-and-test.ps1` فقط بعد نجاح البوابات المطلوبة.

لا ترفع حالة البنود إلى VERIFIED من binding أو mock أو screenshot منفرد. قدم في
النهاية: السبب، الملفات المعدلة، الاختبارات بنتائجها، provider/license matrix،
أدلة T0–T4، وما بقي `PARTIAL/NOT RUN/BLOCKED`.

## معايير القبول النهائية

- لا يطلب GEBCO 2026 أو Copernicus local/imported ion token.
- مصدر GEBCO/Copernicus لا يظهر Active قبل وجود data/tiles صالحة وprobe عددي.
- Cesium ion providers منفصلة بالاسم والمتطلبات والحصة.
- `Browse All` يفتح Basemap pane الموحدة داخل Options ولا توجد نافذتان مختلفتان
  للاختيار العام.
- fresh install يفتح على `esri-hybrid` بالـlabels؛ saved valid choice محترم؛
  offline fallback صادق ولا يفسد الإعداد.
- الشبكة والـlabels ثابتة أثناء pan/zoom/tilt/rotate، بلا خطوط خلف الكرة، وبصيغة
  صحيحة عند خطوط التاريخ والاستواء والقطبين.
- Elevation/Profile مصدرهما DEM رقمي معلن، والمبالغة الرأسية لا تغيّر القياس.
- English وArabic وRTL وDPI والkeyboard paths مختبرة.
- لا Preview ولا ادعاء completion قبل أدلة المستوى المطلوب.

## مصادر رسمية يجب إعادة التحقق منها وقت التنفيذ

- GEBCO 2026: https://www.gebco.net/data-products/gridded-bathymetry-data/gebco2026-grid
- Copernicus DEM: https://dataspace.copernicus.eu/explore-data/data-collections/copernicus-contributing-missions/collections-description/COP-DEM
- Copernicus CCM access: https://documentation.dataspace.copernicus.eu/Data/Others/CCM.html
- AWS Terrain Tiles: https://registry.opendata.aws/terrain-tiles/
- NASA SRTM: https://www.earthdata.nasa.gov/centers/lp-daac
- JAXA AW3D30: https://www.eorc.jaxa.jp/ALOS/en/dataset/aw3d30/aw3d30_e.htm
- Cesium terrain: https://cesium.com/learn/cesiumjs-learn/cesiumjs-terrain/
- Cesium ion token security: https://cesium.com/learn/ion/cesium-ion-access-tokens/

## نتيجة التنفيذ — 28 أكتوبر 2026

- `aw3d30`: JAXA Earth API الرسمي، مباشر بلا حساب أو مفتاح، مع probe رقمي قبل
  إعلان التفعيل.
- `copernicusGlo30`: CDSE Process API الرسمي، مجاني مع OAuth قصير العمر مستقل
  عن Cesium ion ولا يُحفظ في `settings.json` أو السجلات.
- `gebco2026`: الشبكة الرقمية الرسمية من CEDA OPeNDAP بلا مفتاح، وليست WMS.
- `srtm30`: بلاطات AWS/Mapzen الرقمية بلا مفتاح، بالاسم الصادق
  `SRTM-backed` لأن الفسيفساء تستخدم مصادر DEM عامة أخرى خارج مناطق SRTM.
- `localDem`: GeoTIFF/VRT رقمي محلي مع فحص CRS وNoData والوحدة الرأسية.
- Basemap موحدة داخل تبويب `Basemap & Display`، والافتراضي الجديد
  `esri-hybrid` مع احترام اختيار المستخدم المحفوظ.
- إعادة بناء شبكة الإحداثيات أصبحت مرتبطة أيضًا بحدود العرض واتجاه الكاميرا
  وحجم canvas، مع `ResizeObserver` لمنع التسميات القديمة أو المختفية عند تغيير
  الحجم.

### دليل التنفيذ

- T0/T1/T2: `dotnet build` نجح دون تحذيرات، MSTest نجح `262/262`، واختبارات
  Node نجحت `68/68`، و`node --check` نجح.
- فحص endpoints قراءة فقط: JAXA module وAW3D30 collection وGEBCO CEDA أعادت
  HTTP 200؛ وكتالوج AW3D30 أعاد CORS يسمح لأصل WebView2.
- T3 جزئي: تشغيل Release الفعلي حمّل WebView2/Cesium وبلغ حالة `Ready`، لكن
  أداة الالتقاط أعادت إطارًا أسود وتعارض تحديث المؤشر المستمر مع أتمتة الضغط؛
  لذلك لم تُرفع رؤية التضاريس/labels أو اتصال Copernicus إلى VERIFIED.
- T4: لم يُنشأ Preview، التزامًا بمنع تصنيع إصدار قبل اكتمال القبول البصري
  واتصال المصادر الحية المطلوبة.

