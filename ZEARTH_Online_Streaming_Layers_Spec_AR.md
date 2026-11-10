# Z-EARTH Desktop — شجرة الطبقات والبث المباشر Online Streaming على نمط Google Earth Pro

**نوع المستند:** مواصفة تنفيذية محدّثة (Online Streaming Layer Tree / Provider Registry / Data Quality / Bandwidth Control)

**تاريخ مراجعة المراجع:** 10 نوفمبر 2026  
**الحالة:** خطة/مواصفة — **الإصدار 2: البث المباشر إلزامي بدل تنزيل قواعد البيانات لدى المستخدم**؛ لا تمثل تنفيذًا فعليًا.  
**النطاق:** إعادة إنشاء **الفكرة والسلوك الوظيفي** لشجرة Layers التي تظهر في لقطة Google Earth Pro ببيانات مرخصة ومستقلة تُعرض مباشرة عبر الإنترنت عند الطلب؛ **لا تنزيل Shapefile/GeoPackage/PBF أو حزم دول/مدن إلى جهاز المستخدم**. لا تُنسخ بيانات Google Earth المحمية.

---

## 0. القرار الهندسي الملزم — البث المباشر فقط (Online Streaming First)

**طلب صاحب المشروع:** المستخدم يضغط على checkbox في `Layers` فتظهر الطبقة فورًا على الكرة، بالاعتماد على الشبكة مثل Google Earth Pro، **دون مطالبته بتنزيل أو استيراد ملفات طبقات ضخمة، ودون مزامنة كاملة للدول تلقائيًا على جهازه.** قد تنقل الشبكة بلاطات صغيرة مؤقتًا للعرض، وهذا استهلاك إنترنت لا يمكن إلغاؤه عند استعراض مناطق جديدة.

### 0.1 ما المسموح والممنوع؟

| المسألة | السياسة | لماذا؟ |
|---|---|---|
| الحصول على الطبقة للمستخدم | Streaming عند تفعيلها وعند الحاجة فقط | لا Download Dataset، ولا إعداد يدوي |
| حركة الكاميرا/Zoom | اجلب فقط البلاطات التي تتقاطع مع المشهد الحالي مع هامش صغير جدًا عند الحاجة | خفض الاستهلاك وزمن الانتظار |
| تعطيل checkbox | أوقف الطلبات الجديدة وألغِ الطلبات المعلقة للطبقة، وأزل رسمها | منع استهلاك الشبكة بالخلفية |
| بدء تشغيل البرنامج | لا تبدأ جلب كل الطبقات؛ اسمح فقط بما يلزم Basemap الافتراضي | Startup خفيف |
| الـCache | ذاكرة/Cache محدود وفق رخصة كل مصدر وHTTP headers؛ يمكن مسحه | عرض أسرع، وإعادة استخدام بدل إعادة الطلب |
| تنزيل ملفات `osm.pbf`, `.shp`, `.gpkg`, `GeoJSON` عالمي | **ممنوع في تطبيق العميل لأغراض عرض الطبقات الافتراضية** | لا حاجة لتحميل قاعدة الدولة كاملة |
| بناء فهارس وتحويل البيانات | **على خادم المطوّر/البنية المستضيفة فقط** إذا لم يوجد موفر Tiles جاهز | الحفاظ على هندسات الأصل وجودة LOD دون عبء على المستخدم |
| عدم وجود إنترنت | Globe/طبقة محلية خفيفة إن كانت متضمنة بشكل قانوني؛ باقي الخدمات `Offline` | لا تدّعِ إظهار حدود جديدة بلا بيانات |
| موفر محدود أو مدفوع | لا تفعّله دون ترخيص واختبار حصص الاستخدام، وقد يحتاج تمويل استضافة | مجاني البيانات ≠ مجاني الاستضافة إلى الأبد |

### 0.2 معمارية العرض

```text
[Z-EARTH Desktop / Layers Tree]
         │ checkbox + camera bbox + zoom + time
         ▼
[Layer Controller + Provider Registry]
         │ lazy-load, dedupe, cancellation, source switch, visible attribution
         ▼
[Tile Request Manager / metered-network policy]
         │ only visible tile keys + bounded concurrency + cache checks
         ├────────► [Direct legal WMTS/XYZ/WMS/3D Tiles API]
         │
         └────────► [Z-EARTH controlled CDN / tile server]
                                ▲
                  [Server-side ingestion and QA only]
                    geoBoundaries / Overture / OSM
         ▼
[Cesium globe renderer / actual project engine]
```

**قاعدة تمييز أساسية:** `geoBoundaries` و`Overture Maps` و`Geofabrik` و`Google Open Buildings` **مصادر بيانات**، وليست بالضرورة خدمات بث Tiles جاهزة لكل الطبقات. واجهة geoBoundaries API تُرجع بيانات وصفية وروابط تنزيل هندسات؛ فلا تُربط مباشرة بخانة اختيار حدود لتبدأ تنزيل دولة كاملة على جهاز العميل. يتم إعداد ملفات عرض مجزأة متعددة الدقة على الخادم/سحابة Z-EARTH، وتقديمها عبر `TileJSON + MVT`, أو `PMTiles` مستضاف يدعم HTTP Range، أو `3D Tiles`، أو `WMTS/XYZ` حسب نوع الطبقة والمحرك. لا يُقدَّم عنوان API خاص بالتنزيل على أنه Tiles endpoint.

**التوافق:** إذا كان محرك العرض الحالي CesiumJS فلا تفترض دعمه المباشر لـ MVT/PMTiles مثل MapLibre؛ افحص قدرات المحرك والجسر الحالي، وأنشئ adapter/تحويلًا معتمدًا يحافظ على جودة الحدود والتسميات. استخدم `Cesium3DTileset` للطبقات المناسبة و`UrlTemplateImageryProvider`/`WebMapTileServiceImageryProvider` للخرائط الشبكية وفق قدرات النسخة الموجودة. يجب اختبار كل مسار على الكود الحقيقي قبل الإقرار بنجاحه.

### 0.3 الضوابط العملية لاستهلاك الإنترنت

1. **Visible tiles only:** حساب البلاطات الفعلية المرئية حسب مجال الرؤية والتكبير؛ لا مسح كامل لدولة ولا تحميل مستويات Zoom غير مطلوبة.
2. **Progressive detail:** عرض مستوى عام سريع ثم تحسين التفاصيل عند ثبات الكاميرا؛ منع جلب هندسة عالية الدقة للعالم كله في لقطة واحدة.
3. **Abort and debounce:** إلغاء الطلبات القديمة عند دوران أو تكبير أو إخفاء طبقة؛ دمج الطلبات المكررة وإعطاء أولوية للبلاطات المرئية فعلًا.
4. **Bandwidth controls:** `Data Saver` اختياري؛ تقليل دقة الصور/3D، تعطيل الـprefetch، حد لعدد الطلبات المتزامنة، وإيقاف الطبقات الثقيلة عند الاتصال المحدود **بموافقة المستخدم**.
5. **Cache compliant:** احترم سياسات التخزين الخاصة بكل مزود. خوادم OSM العامة تتطلب اتّباع ترويسات الكاش (أو مدة أدنى محددة في السياسة)، وتمنع تنزيل المناطق للتخزين المسبق، كما أن الخدمة دون SLA؛ لا تُستخدم كأساس إنتاجي وحيد. لا تفرض `no-cache` دائمًا.
6. **Observability:** لكل طبقة اعرض `Online / Loading / Ready / No coverage / Rate limited / Failed / Offline`، وتاريخ بيانات المصدر، وعدد البايتات المنقولة في جلسة الاستخدام (إن أمكن القياس دون ازدواج).
7. **No hidden traffic:** الطبقات غير المفعّلة لا تنشئ حركة مرور؛ لا تمرر البيانات إلى تبويب Gallery أو My Places قسرًا؛ لا يجري Sync خفي للدول.
8. **Attribution & access:** تظهر نسب البيانات المطلوبة باستمرار؛ تحدّث أو عطّل موفرًا غيّر شروط الوصول، ولا تحوّل API مجانيًا محدودًا إلى خدمة غير مرخّصة على حساب المستخدم.

### 0.4 نوع الخدمة المناسب لكل طبقة

| فرع Layers | اتصال مناسب وقت العرض | التحضير خارج جهاز المستخدم |
|---|---|---|
| Country / Governorate Borders, Labels | Vector Tiles عبر خدمة مؤهلة / Tiles من خادم Z-EARTH | دمج وفحص حدود مرجعية أصلية غير معممة، ثم توليد مستويات عرض |
| Roads, Transportation, Places, Parks | Vector Tiles مع طلب POIs عند التكبير المناسب | مصدر OSM/Overture مع التحديث والفلترة على الخادم |
| Buildings 2D / 2.5D | Vector Tiles هندسة + ارتفاعات موثقة | تحويل مصدر footprints على الخادم |
| 3D Photorealistic | مرخّص `3D Tiles` متدفق بحسب المشهد | يلزم مصدر مدينة/مزود خدمة يدعم الترخيص؛ لا بديل مجاني عالمي مضمون |
| Clouds / Satellite data | NASA GIBS `WMTS/WMS/XYZ` مع اختيار الزمن | الاستعلام عن المنتجات المتاحة والحدود الزمنية |
| Historical Maps | `WMTS/WMS/IIIF` أو Tiles مرخصة لكل خريطة | تسجيل حقوق وextent والتاريخ لكل خريطة؛ لا نسخ مكتبة كاملة |
| Water / Terrain / Bathymetry | Terrain tiles / COG window access / خدمات مرخصة | تجهيز منتجات هرمية على الخادم عندما لا توجد خدمة جاهزة |
| Photos / Wikipedia | APIs بحث مكاني بحدود `bbox` وpagination | روابط/صور مرخصة مفردة عند النقر، لا تنزيل مكتبة صور |

**ليس هناك ضمان لاستضافة مجانية بلا حدود.** يمكن جعل الطبقات مجانية للمستخدم، بينما تتحمل جهة تشغيل Z-EARTH كلفة الخادم/النقل أو تستخدم خدمات عامة ضمن حصصها وترخيصها. القدرة على العرض العالمي عالي الجودة تتطلب خطة استضافة واستمرارية، لا مجرد مصدر بيانات مفتوح.

### 0.5 اختبارات قبول وضع البث

- [ ] تثبيت نسخة Z-EARTH دون حزم حدود/طرق/مبانٍ لدول، وعدم ظهور طلب تنزيل ملف دولة عند تشغيل checkbox.
- [ ] تشغيل `Country Borders` في مصر يعرض حدودًا صحيحة من Tiles متدفقة مع `source/year/license`، ويزيد التفصيل عند التكبير **فقط إن توفر مصدر دقيق**.
- [ ] مراقبة Network: تحميل المناطق المرئية فقط، وعدم طلب أي بيانات طبقة مخفية، وإلغاء الطلبات القديمة بعد الحركة.
- [ ] عند تعطيل طبقة مبانٍ أو طرق تختفي فورًا ولا تستمر طلبات الشبكة الخاصة بها.
- [ ] الاستعراض المتكرر يستفيد من cache المتوافق مع الرخصة، دون طلب نفس الموارد بلا داعٍ.
- [ ] تفعيل وضع توفير البيانات لا يكسر عمل طبقات الحدود، بل يقلل الصور/المجسمات الثقيلة.
- [ ] سقوط الخدمة أو نفاد الحصة ينتج رسالة صريحة ومصدرًا بديلًا **مرخّصًا ومختبرًا**، لا مجرد checkbox أخضر.
- [ ] إظهار حجم البيانات المنقولة أثناء التجربة إن كان قابلًا للقياس من الكود، ولا تعلن نسب توفير غير مقاسة.

---

## 1. الملخص التنفيذي والقرارات

1. يمكن بناء شجرة طبقات أشمل من شجرة Google Earth Pro، ببيانات مفتوحة وقابلة للتحديث. **لكن لا يوجد مصدر مجاني واحد يحقق عالميًا نفس دقة كل طبقات Google، ولا بديل مفتوح مطابق عالميًا لصور Google الواقعية ثلاثية الأبعاد.**
2. **تجنب الاعتماد على Natural Earth كطبقة حدود دقيقة عند التكبير.** إن نطاقات 1:10m و1:50m و1:110m التي يعلنها هي مقاييس خرائط كارتوغرافية معممة، وليست دقة مكانية بالمتر.
3. لحدود الدول والتقسيمات الإدارية: **geoBoundaries gbOpen + Overture Divisions + OSM extracts**، مع تقديم أي بيانات حكومية محلية أعلى جودة ومرخصة بعد التحقق منها. لا نفترض أن أحدها أكثر دقة في جميع البلدان.
4. للطرق والأماكن: **Overture Transportation / Places + OpenStreetMap**؛ للمباني: **Overture Buildings / Google Open Buildings / Microsoft Global ML Building Footprints**، مع تمييز footprint عن نموذج ثلاثي الأبعاد واقعي.
5. لإنشاء نسخة مستقرة في تطبيق Windows: **احتفِظ بمصادر التحليل كاملة الهندسة على الخادم/بيئة التحضير فقط (GeoPackage/GeoParquet/GeoJSON)**، وقدّم **vector tiles / 3D Tiles / WMTS** عبر الإنترنت للعميل. لا توزّع ملفات الأصل أو MBTiles محلية مع التطبيق؛ PMTiles مسموح عندما يكون **مستضافًا** ويُطلب جزئيًا عبر HTTP Range.
6. لتنفيذ طبقات الطقس/الصور/الخرائط التاريخية: NASA GIBS، Open-Meteo بشروط استعماله، Wikimedia Commons، Panoramax، David Rumsey مع التحقق من حقوق كل خريطة، Library of Congress وCopernicus STAC.
7. ميّز بوضوح بين: **مجاني التحميل**، و**مفتوح الترخيص**، و**مسموح تضمينه في برنامج موزع/تجاري**، و**خدمة مجانية بمعدل طلبات محدود**؛ هذه أمور مختلفة.

> **ملاحظة مهمة عن Google Earth نفسه:** الطبقات الظاهرة ليست ملفات Shapefile قابلة للنسخ، بل محتوى يُخدم من Google ومن شركائها ومصادر تاريخية متفاوتة. تفاصيل مزود كل طبقة/موقع ليست مكشوفة دائمًا عبر واجهة Pro. كتالوج طبقات Google Earth الأحدث مختلف عن شجرة Pro القديمة، ويعرض بيانات المصدر والقيود لكل طبقة. تصدير KML من Google Earth لا يمنح حق نسخ طبقاته المحمية؛ يُمنع استخراجها لاستخدامها كأساس لمنتج بديل.

---

## 2. قراءة الصورة المرفقة: كل عقدة ومقابلها في Z-EARTH

| الفرع الظاهر في Google Earth Pro | ما الذي يمثله عمليًا؟ | مصدر/بديل مجاني أو مفتوح مقترح | قابلية المطابقة |
|---|---|---|---|
| **Google Earth web** | رابط/مدخل لخدمة Google Earth على الويب، لا طبقة حدود | Open in Browser (رابط اختياري) / مشروع Z-EARTH Web مستقبلًا | ميزة واجهة، وليست طبقة GIS |
| **Borders and Labels → Borders** | حدود دول ومناطق | geoBoundaries / Overture Divisions / OSM / الجهات الرسمية | جيد جدًا إقليميًا بعد التحقق؛ متغير عالميًا |
| **Borders and Labels → Labels** | أسماء الدول والمدن والمناطق | Overture Divisions / OSM names / Wikidata؛ تسميات ثنائية اللغة | مرتفع مع معالجة مواضع التسميات |
| **Borders and Labels → Places** | أسماء ومعالم/أماكن مأهولة | Overture Places / OSM POIs / Wikidata | مرتفع نسبيًا، متفاوت التغطية |
| **Photos → Google Maps Photos** | صور جغرافية مرتبطة بأماكن | Wikimedia Commons GeoSearch / Panoramax / صور المستخدمين | مرتفع جزئيًا، وليس قاعدة صور Google |
| **Roads** | طرق وشبكة مواصلات | Overture Transportation + OSM/Geofabrik | مرتفع حيث تتوفر بيانات جيدة |
| **3D Buildings → Photorealistic** | مبانٍ ومشاهد فوتوغرامترية واقعية ثلاثية الأبعاد | نماذج 3D Tiles مفتوحة حسب المدينة، أو بناء 2.5D من المباني والارتفاعات | **لا يوجد بديل مفتوح عالمي مطابق** |
| **3D Buildings → Trees** | أشجار/غطاء نباتي ضمن 3D | OSM tree positions / غطاء نباتي محلي/بيانات LiDAR مرخصة | متفاوت؛ لا تتوقع شجرة لكل موقع |
| **Weather → Clouds** | صور/تصور السحب | NASA GIBS صور أقمار زمنية؛ منتجات سحب مناسبة | جيد لرصد السحب من صور الاستشعار، لا بث مباشر مستمر |
| **Weather → Information** | حالة/توقعات الطقس | Open-Meteo مع قيود المجاني غير التجاري أو نماذج NOAA المفتوحة | جيد حسب النموذج والموقع |
| **Gallery → Everytrail** | مسارات ورحلات من تكامل تاريخي | OSM hiking/cycling + GPX المستخدم + منصات تتيح API مرخصًا | ميزة بديلة، وليس إعادة استعمال الخدمة القديمة |
| **Gallery → Gigapan Photos** | صور بانورامية | Wikimedia Commons panoramas / Panoramax | جزئي؛ اختلاف المحتوى |
| **Gallery → Google Earth Community** | محتوى مجتمع المستخدمين | كتالوج KML/KMZ مجتمعي خاص بالمشروع مع مراجعة أمنية | قابل للبناء من الصفر |
| **Gallery → Rumsey Historical Maps** | خرائط David Rumsey التاريخية المسندة جغرافيًا | David Rumsey مباشرة حيث يسمح الحق، وLOC بمحتوى مرخص | مصدر أصلي معروف ومتوافر للفحص |
| **Gallery → Map Finder** | العثور على خرائط تاريخية أو موضوعية | فهرس الخرائط حسب الامتداد والتاريخ وSTAC/IIIF | يمكن إنشاء بديل أوسع |
| **Gallery → Trimble Outdoors Trips** | مسارات/رحلات ضمن تكامل قديم | GPX + OSM routes + مسارات محلية مرخصة | بديل وظيفي؛ ليس نفس الخدمة |
| **More → Parks/Recreation Areas → Parks** | متنزهات حضرية | OSM `leisure=park` | جيد حسب المنطقة |
| **→ Golf Courses** | ملاعب الجولف | OSM `leisure=golf_course` / Overture Places | جيد متفاوت |
| **→ US Fish and Wildlife Service boundary** | حدود مناطق هيئة أمريكية | USFWS datasets الرسمية، ومصادر USGS للمرجع | جيد داخل الولايات المتحدة |
| **→ USDA Forest Service** | غابات ومناطق اتحادية أمريكية | USDA Forest Service / USGS الرسمية | جيد داخل الولايات المتحدة |
| **→ US National Parks** | حدود المتنزهات الوطنية الأمريكية | National Park Service / USGS؛ ثم OSM عالميًا | جيد داخل نطاقه |
| **More → Transportation** | طبقات مواصلات فرعية | Overture Transportation / OSM Rail/Airport/Ferry | مرتفع نسبيًا |
| **More → Water Body Outlines** | سواحل وبحيرات وأنهار | OSM + Overture Base + بيانات هيدرولوجية وطنية؛ Natural Earth للمقاييس الصغيرة | جيد جدًا حسب المقياس |
| **More → Wikipedia** | مقالات موسوعية ذات إحداثيات | Wikipedia/MediaWiki GeoSearch API + Wikidata | بديل مباشر مشروع |
| **More → US Government → City Boundaries** | حدود مدن الولايات المتحدة | U.S. Census TIGER/Line / USGS National Map | رسمي ومتخصص |

**ملاحظات:** الفرع المقطوع أسفل الصورة غير مشمول. بعض العناصر داخل Gallery تعكس تكاملات تاريخية؛ لا تُعرض في Z-EARTH بصفتها اتصالات فعالة دون اختبار endpoints والشروط. توثيق Google الرسمي يقرر أن محتوى هذه الطبقات قد يتغير، وأن المصادر والحقوق تختلف حسب الطبقة.

---

## 3. كتالوج المصادر الرئيسية وترخيص الاستخدام

| المعرف | المصدر والرابط الرسمي | أنواع الطبقات | الوصول والتحديث | ملاحظات قانونية وجودة |
|---|---|---|---|---|
| `GEOB_OPEN` | [geoBoundaries](https://www.geoboundaries.org/) / [API](https://www.geoboundaries.org/api.html) | ADM0–ADM5 حيث متاح | تنزيل API/GeoJSON/Shapefile؛ يسجل سنة وصاحب المصدر | gbOpen مرخص للنسبة؛ راجع metadata الخاصة بكل ملف؛ لا تستخدم gbAuthoritative/gbHumanitarian تلقائيًا بنفس رخصة gbOpen |
| `OVERTURE_DIV` | [Overture Divisions](https://docs.overturemaps.org/guides/divisions/) | دول، محافظات، مدن، أحياء؛ area/boundary | GeoParquet على S3/Azure، إصدارات دورية | ODbL؛ المصدر يدمج OSM وgeoBoundaries، وقد يضم وجهات نظر حدودية مختلفة |
| `OVERTURE_TRANS` | [Overture Docs](https://docs.overturemaps.org/) | طرق، مسارات، نقل | إصدارات منتظمة | غالبًا ODbL على theme Transportation؛ طبّق attribution وشروط التراخيص على الإصدار المحدد |
| `OVERTURE_PLACES` | [Overture Places](https://docs.overturemaps.org/guides/places/) | نقاط اهتمام/مؤسسات ومعالم | GeoParquet منتظم | ترخيص متعدد حسب المصدر (CDLA permissive / Apache)؛ ليس نفس التزام ODbL بالضرورة |
| `OVERTURE_BUILDINGS` | [Overture Buildings](https://docs.overturemaps.org/guides/buildings/) | بصمات المباني والارتفاعات حيث توفرت | GeoParquet منتظم | جودة بصمات التعلم الآلي متغيرة؛ ليس photogrammetry |
| `OSM_GEOFABRIK` | [Geofabrik](https://www.geofabrik.de/data/download.html) | طرق، مبانٍ، حدود، ماء، أماكن | extracts تُحدّث غالبًا يوميًا | ODbL + ذكر OSM؛ الأفضل تنزيل PBF ومعالجته محليًا بدل إرهاق خوادم OSM العامة |
| `GOOGLE_OPEN_BUILDINGS` | [Google Research Open Buildings](https://sites.research.google/gr/open-buildings/) | بصمات مبانٍ في إفريقيا وجنوب العالم | تنزيل بلاط جغرافي/CSV، إصدار بيانات 2023 v3 | CC BY 4.0 أو ODbL حسب خيار المستخدم؛ **ليست صور Google ولا نماذجها الواقعية**. تحتوي درجة ثقة؛ راجع حد الثقة وتاريخ صور المصدر |
| `MS_BUILDINGS` | [Microsoft Global ML Building Footprints](https://github.com/microsoft/GlobalMLBuildingFootprints) | مبانٍ عالمية، وبعض تقديرات الارتفاع | ملفات إقليمية، تحديثات 2026 موثقة | المستودع الحالي يذكر CDLA Permissive 2.0؛ تحقق من إصدار المصدر بدل الاعتماد على تراخيص إعادة نشر قديمة |
| `NATURAL_EARTH` | [Natural Earth](https://nacis.org/initiatives/natural-earth/) | حدود وسواحل وأماكن وماء للعرض العام | ملفات جاهزة على 1:10m/50m/110m | Public Domain؛ **للـoverview وليس لحدود جغرافية دقيقة محليًا** |
| `NASA_GIBS` | [NASA GIBS APIs](https://nasa-gibs.github.io/gibs-api-docs/access-basics/) | imagery وسحب ومظاهر بيئية زمنية | WMTS / WMS / TWMS / XYZ عبر كتالوج المنتجات | استخدم معرف layer موثقًا وتاريخًا مدعومًا؛ راجع attribution وقيود كل منتج |
| `OPEN_METEO` | [Open-Meteo](https://open-meteo.com/en/docs) | طقس وتوقعات/تاريخ/بحر | REST API | الواجهة العامة مجانية **للاستخدام غير التجاري** بشروط وحدود استخدام؛ تحتاج خطة/ترخيص ملائم للتجاري |
| `COP_DEM` | [Copernicus DEM on AWS](https://registry.opendata.aws/copernicus-dem/) | ارتفاعات أرضية | COG عبر AWS، GLO-30 Public / GLO-90 | GLO-30 Public تغطية عامة غير مكتملة بكل الدول؛ DEM هو DSM يشمل مباني/غطاء نباتي؛ ليس ارتفاع أرض عارية مضمونًا |
| `GEBCO_2026` | [GEBCO_2026 Grid](https://www.gebco.net/data-products-gridded-bathymetry-data/gebco2026-grid) | أعماق البحار + تضاريس الأرض | netCDF / GeoTIFF، 15 arc-seconds | مجاني، عام، **يمنع الاعتماد الملاحي**؛ بعض الأعماق متنبأ بها وليست قياسات مباشرة |
| `WIKIMEDIA` | [Wikimedia Commons](https://commons.wikimedia.org/) / [MediaWiki GeoSearch](https://www.mediawiki.org/wiki/API:Geosearch) | صور ومعالم وإحداثيات | API | كل صورة لها ترخيصها وصاحبها؛ لا تفترض أن كل الصور CC0 |
| `PANORAMAX` | [Panoramax](https://docs.panoramax.fr/backend/api/api/) | صور طرق بانورامية/موقعية | STAC API على instances | تراخيص الصور تختلف بين instance وأخرى؛ راجع CC BY-SA/غيره |
| `RUMSEY` | [David Rumsey Historical Maps](https://www.davidrumsey.com/view/google-earth) / [Permissions](https://www.davidrumsey.com/about/copyright-and-permissions) | خرائط تاريخية مسندة جغرافيًا | تنزيل/روابط بحسب الخريطة | إذن الاستعمال يختلف حسب عمر الخريطة والحقوق؛ **لا تجعل كل الموقع Tiles متاحة لإعادة التوزيع افتراضيًا** |
| `LOC_MAPS` | [Library of Congress APIs](https://www.loc.gov/apis/) | أرشيف خرائط وصور تاريخية | JSON وIIIF | ترخيص/حقوق كل عنصر منفردًا؛ بعض المواد بالملكية العامة |
| `EUROSTAT_GISCO` | [GISCO Geodata](https://ec.europa.eu/eurostat/web/gisco/geodata/) | حدود ومستويات NUTS | تنزيل/توزيع API | قواعد وتواريخ محددة لكل نسخة؛ مصدر تخصصي أوروبي |
| `USGS_TNM` | [USGS The National Map](https://www.usgs.gov/tools/download-data-maps-national-map) | حدود/طرق/ارتفاعات/ماء/تاريخ | تنزيل وخدمات خرائط | بيانات حكومية أمريكية متخصصة؛ تحقق من metadata الخاصة بالمنتج |
| `COP_STAC` | [Copernicus Data Space STAC](https://documentation.dataspace.copernicus.eu/APIs/STAC.html) | Sentinel-2/Sentinel-1 وغيرها | STAC API | فرق واضح بين *صور رصد* قابلة للمعالجة و*طبقة basemap* جاهزة؛ قد يحتاج المعالجة/تسجيلًا وفق خدمة الوصول |
| `OSM_PUBLIC_TILES` | [OSMF Tile Usage Policy](https://operations.osmfoundation.org/policies/tiles/) | خرائط OSM raster جاهزة | HTTPS tiles وفق السياسة | لا bulk download ولا offline prefetch، ولا SLA؛ **لا تعتمد عليه كمزود الإنتاج الوحيد** |
| `NOMINATIM_PUBLIC` | [Nominatim Usage Policy](https://operations.osmfoundation.org/policies/nominatim/) | بحث جغرافي نصي | API محدود جدًا | 1 طلب/ثانية كحد أقصى للخدمة العامة؛ يحظر autocomplete عليها؛ الأفضل self-host أو خدمة مرخصة مناسبة |

**مستبعد تلقائيًا من حزمة التوزيع الافتراضية:** GADM رغم دقته في مناطق كثيرة لأن [الرخصة](https://gadm.org/license.html) تقيد الاستعمال التجاري وإعادة التوزيع؛ Protected Planet/WDPA لأن [بياناته](https://resources.unep-wcmc.org/products/WCMC_PT001) تفرض قيودًا على إعادة التوزيع والتجاري. يمكن توفيرهما كمصدر اختياري يضيفه المستخدم وفق ترخيصه الشخصي، دون إدراج بياناتهما مع التطبيق.

---

## 4. تصميم «نظام دقة الحدود» — الإلزام الفني الأهم

### 4.1 افصل الدقة عن التكبير

- عبارة «1:10m» في Natural Earth تعني تقريبًا **1:10,000,000** (مقياس الخريطة)، **ولا تعني 10 أمتار**. هي خطوط معممة عمدًا؛ لا ينبغي مدها حتى مستوى شوارع/قطع أراضٍ.
- لا توجد قاعدة أمينة تقول «geoBoundaries أدق دائمًا من OSM» أو العكس. الدقة تحددها **جهة المصدر، مقياس تجميع الأصل، تاريخ التحديث، سلامة الهندسة، المنطقة والهدف**.
- **Visual detail** ليست **Positional accuracy**. يمكن لحد يبدو سلسًا على الخريطة أن يكون خاطئًا؛ ويمكن لحد رسمي أن يبدو خشنًا لأن مصدره منخفض الدقة.
- اختلاف خطوط الحدود بين المصادر قد ينتج عن **نزاع سياسي أو مرجع حدودي مختلف**، وليس خطأ برمجيًا. لا تدمج حدود طرفين أو تستبدلها بصمت.

### 4.2 سياسة اختيار المصدر

| سياق العرض | مصدر أولي | المصدر التالي | سلوك مطلوب |
|---|---|---|---|
| رؤية عالمية / الكرة كاملة | Natural Earth (public-domain overview) | Overture generalized | رسم سريع قليل الأعباء؛ شارة "Overview / Generalized" |
| قارات/دول | Overture / geoBoundaries بعد فحص الجودة | OSM extracts | خطوط مُحسّنة للعرض تُبثّ كبلاطات؛ الأصل محفوظ على الخادم فقط |
| تكبير دولة/محافظة/مدينة | بيانات حكومية مرخصة أو geoBoundaries gbOpen **الكاملة غير المبسطة** | Overture/OSM وفق metadata | اختيار الجودة حسب البلد وإظهار سنة المرجع |
| مستوى تفصيلي جدًا | حدود وطنية/محلية تفصيلية إن وجدت | OSM، وإلا إظهار آخر مصدر صالح بوسم حدود دقة العرض | **ممنوع** رفع الوضوح اصطناعيًا أو ادعاء دقة 1 متر |
| نطاق متنازع عليه | boundary perspective/claims explicit | مصادر مستقلة | عرض المصدر/وجهة النظر/حدود متنازع عليها اختيارًا، دون تقرير سيادة |

**تنفيذ طبقات Overture:** `division_area` (مضلعات)، و`division_boundary` (حدود مشتركة)؛ لا تولّد حدودًا متطابقة مرتين من حواف مضلعات جارين. راعِ `is_land` و`is_territorial` عند الفصل بين حدود اليابسة والحدود البحرية.

### 4.3 سجل جودة لكل حدود

لكل dataset اجمع واحفظ:

```yaml
id: country_admin0_egy_example
provider: GEOB_OPEN
source_url: "https://www.geoboundaries.org/api/current/gbOpen/EGY/ADM0/"
country_iso3: EGY
admin_level: ADM0
source_authority: "from provider metadata; do not invent"
boundary_year: "from boundaryYearRepresented"
server_ingest_date_utc: "actual ingest timestamp on provider/hosting server"
release_id: "pinned or actual provider version"
crs: "verified native CRS"
original_geometry_server_only: "private validated source, never sent to client"
display_tile_service: "validated streamed tile service; not a dataset download URL"
license: "from boundaryLicense/license metadata"
attribution: "required and visible"
geometry_validated: false
positional_accuracy_m: null   # لا تضع رقمًا تخمينيًا
status: "pending QA"
```

### 4.4 QA إلزامي قبل اعتماد الحدود

1. **Topology:** فحص valid geometries، اتجاه الحلقات، self-intersections، gaps/overlaps غير المقصودة، خطوط الطول عند ±180، الجزر والجيوب، والقطبين؛ لا تحذف أجزاء بحجة تحسين الأداء.
2. **CRS:** قراءة CRS من المصدر أو إثباته؛ واجهة العرض WGS84/Cesium مع transform صحيح؛ لا تتعامل مع خطوط الطول/العرض كأمتار عند التبسيط.
3. **Linework:** مقارنة عينات حدودية مع جهة رسمية مرخصة في 3 دول/مناطق مختلفة على الأقل؛ تسجيل الفارق بالأمتار عندما يوجد مرجع دقيق صالح. لا تضع قيمة accuracy افتراضية.
4. **Scale QA:** اختبار Globe → Country → City مع حفظ الثبات البصري؛ اختيار LOD مع hysteresis لمنع «قفز» الخط عند الانتقال بين المصادر؛ زيادة الكثافة عند التكبير لا تعني زيادة الدقة المرجعية.
5. **Names:** دعم الأسماء العربية/الإنجليزية والأسماء البديلة والتسمية المحلية، وربط كل label بمعرّف جغرافي؛ تجنب تداخل التسميات مع التضاريس.
6. **Source/date:** عرض «المصدر / سنة تمثيل الحدود / تاريخ تحديث الخدمة أو معالجة البيانات على الخادم / ترخيصها» في Layer Properties. لا تخلط سنة إصدار ملف البيانات بتاريخ صلاحية الحدود الواقعي.
7. **Disputed borders:** إتاحة أنماط خط متقطع وطبقة اختيارية، وإظهار metadata السياسية للمستخدم؛ لا تفترض وجود حقيقة واحدة متفق عليها لكل قضية.
8. **Failover:** عند تعطل المزود، استخدم cache قانونيًا أو مزود Tiles متدفقًا آخر تم التحقق منه؛ لا تجبر العميل على تنزيل ملفات بديلة.

---

## 5. شجرة Layers المقترحة لـ Z-EARTH (متوافقة مفهوميًا مع الصورة)

```text
Layers
├── Borders & Labels
│   ├── [x] Country Borders                 (ADM0)
│   ├── [ ] State / Governorate Borders     (ADM1)
│   ├── [ ] District / County Borders       (ADM2)
│   ├── [ ] Local Administrative Borders    (ADM3–ADM5 if available)
│   ├── [ ] Disputed / Alternate Boundaries (source-aware)
│   ├── [x] Country Labels
│   ├── [ ] Administrative Labels
│   ├── [x] City & Town Labels
│   └── [ ] Geographic Places
├── Transportation
│   ├── [ ] Major Roads
│   ├── [ ] Local Roads & Streets
│   ├── [ ] Railways
│   ├── [ ] Airports & Ports
│   └── [ ] Trails, Hiking & Cycling
├── Buildings & 3D
│   ├── [ ] 2D Building Footprints
│   ├── [ ] 2.5D Extruded Buildings (height if known)
│   ├── [ ] Verified Open 3D City Models
│   └── [ ] Trees & Urban Vegetation
├── Imagery & Earth Observation
│   ├── [ ] Satellite Imagery Catalog (STAC)
│   ├── [ ] Daily Earth Images (NASA GIBS)
│   └── [ ] Compare Imagery Dates
├── Weather & Atmosphere
│   ├── [ ] Clouds (satellite observations)
│   ├── [ ] Temperature / Precipitation (modeled)
│   └── [ ] Weather Information
├── Photos & Media
│   ├── [ ] Wikimedia GeoPhotos
│   ├── [ ] Open Street-Level Photos (Panoramax)
│   └── [ ] User Photos
├── Historic Maps & Time
│   ├── [ ] David Rumsey Historic Maps (rights-checked)
│   ├── [ ] Library of Congress Historic Maps
│   ├── [ ] Historic Imagery Collections (where licensed)
│   └── [ ] Find Maps by Extent / Period
├── Nature & Recreation
│   ├── [ ] Parks & Gardens
│   ├── [ ] National Parks
│   ├── [ ] Protected Areas (license-checked)
│   ├── [ ] Forests
│   └── [ ] Golf Courses
├── Water & Terrain
│   ├── [ ] Coastlines, Lakes & Rivers
│   ├── [ ] Copernicus Land Elevation
│   ├── [ ] GEBCO 2026 Bathymetry
│   └── [ ] Terrain Source & Exaggeration
├── Knowledge & Government
│   ├── [ ] Wikipedia Geolocated Articles
│   ├── [ ] USGS / US Official Datasets
│   ├── [ ] Eurostat / GISCO Europe
│   └── [ ] Other Licensed National Sources
└── User Data
    ├── [ ] Imported KML / KMZ
    ├── [ ] Imported SHP / GPKG / GeoJSON
    ├── [ ] Imported GeoTIFF / GPX / CSV
    └── [ ] Custom WMS / WMTS / XYZ / STAC (validated)
```

**افتراضيًا:** لا تجعل عشرات الطبقات تحمل عند بدء البرنامج؛ استخدم checkbox لكل فرع وcheckbox ثلاثي الحالة للأب عند تفعيل بعض الأبناء. لا تخلط Layers (خدمات ومصادر) مع My Places/Temporary Places (بيانات يحررها المستخدم). لكل طبقة: **Enable/Disable، Zoom To، Source، Refresh، Legend/Style، Opacity، Date Filter إن وجد، Attribution، Offline Availability، Error/Retry**.

### 5.1 سياسة التعامل مع الواجهة

- الشجرة قابلة للبحث، وإعادة الترتيب Drag & Drop عندما يكون مزج الطبقات منطقيًا، وتفعيل/إخفاء العناصر بشكل مستقل.
- واجهة EN تعرض الإنجليزية فقط، وAR تعرض العربية فقط في نصوص البرنامج؛ أسماء الجهات الرسمية/المنتجات تظل أسماؤها الرسمية عند الحاجة.
- خانة checkbox الحقيقية مرتبطة بحالة محرك الكرة الأرضية **وليس مجرد تغيير شكلي في الشجرة**.
- metadata/license تكشف عبر Layer Properties؛ أعطال الشبكة أو الترخيص تبين للمستخدم، ولا تتحول الطبقة إلى مفعّلة بصريًا بلا رسم فعلي.
- التفريق بين طبقات مستقلة وبين إعدادات Basemap أو Terrain أو Sunlight، حتى لا تتكرر نفس الطبقة بمفاتيح تحكم متعارضة.

---

## 6. Architecture — ربط الشجرة بالمحرك

المرجع التصميمي المرفق للمشروع يصف بيئة Windows تعتمد **WPF MVVM / WebView2 / CesiumJS / GDAL / PROJ**، ويقترح `IProviderRegistry`. نفذ الشجرة كجزء من هذا التصميم إذا ظل قائمًا بعد فحص **الكود الحالي**، ولا تعِد كتابة المحرك لمجرد إضافة الشجرة.

```text
[WPF Explorer: Layers Tree]
            |
[LayersViewModel + Commands]
            |
[ILayerCatalogService]--[IProviderRegistry]--[Provider configs + licenses]
            |
[ILayerLifecycleService] (load / cancel / refresh / fallback)
            |
[GeoData pipeline: sources -> QA -> indexes -> tiles]
            |
[Typed WebView2 Bridge + state acknowledgements]
            |
[CesiumJS globe layer adapters]
            |
[Rendered state + attribution + health/error status]
```

**Contracts المقترحة:**

```csharp
public interface ILayerCatalogService
{
    Task<IReadOnlyList<LayerDescriptor>> GetTreeAsync(CancellationToken ct);
    Task<LayerDescriptor?> GetLayerAsync(string id, CancellationToken ct);
}

public interface ILayerLifecycleService
{
    Task<LayerOperationResult> SetEnabledAsync(string id, bool enabled, CancellationToken ct);
    Task<LayerOperationResult> RefreshAsync(string id, CancellationToken ct);
    Task<LayerHealth> GetHealthAsync(string id, CancellationToken ct);
}

public interface IBoundaryQualityService
{
    Task<BoundaryDecision> SelectProviderAsync(BoundaryRequest context, CancellationToken ct);
    Task<BoundaryValidationReport> ValidateAsync(string datasetId, CancellationToken ct);
}
```

> الأسماء السابقة **عقود تصميمية مقترحة** وليست ادعاء بوجود هذه الأصناف داخل المشروع. عند التنفيذ افحص أسماء الواجهات والـCommands الموجودة أولًا حتى لا تتكون طبقات تكرار.

**بث مباشر ومعالجة على الخادم:**

- `ProviderRegistry` في تطبيق العميل مع **streaming adapters**: WMS/WMTS/XYZ، Vector Tiles/TileJSON، مستودع PMTiles عن بعد بـ HTTP Range، STAC للبحث، COG window requests حيث مدعومة، Cesium imagery/terrain/3D Tiles وفق دعم المحرك الفعلي. **GeoJSON/GPKG/GeoParquet/PBF/MBTiles للتجهيز على الخادم لا لتنزيلها للعميل.**
- **افصل بين بيانات المصدر الكاملة على الخادم وطبقات العرض المتدفقة للعميل**؛ PMTiles يمكن استضافته على خدمة تدعم HTTP Range بحيث يُقرأ الجزء المطلوب فقط، وليس تنزيل ملف PMTiles كاملًا إلى جهاز المستخدم (https://docs.protomaps.com/pmtiles/cloud-storage).
- spatial index + viewport query + level of detail + asynchronous cancellation + ذاكرة cache محدودة + كشف تعطل الشبكة دون freeze.
- استخدم `source_id` ثابتًا و`layer_id` ثابتًا و`feature_id` متسقًا عند تبديل المصدر؛ هوية العنصر لا تعتمد على اسم التسمية وحده.
- **ترخيص المصدر جزء من contract.** لا يُسجَّل مزود تلقائيًا ما لم تتوفر طريقة attribution وشروط الوصول، وتفاصيل offline/cache والتوزيع.
- **الثقة والأمان:** منع حقن KML/HTML في popup؛ التحقق من URLs وMime/Size؛ لا تخزن tokens في `.zearth`، وتطبق سياسات CORS/allowlist وWebView2 origin validation.

### مثال سجل JSON للطبقة

```json
{
  "id": "administrative.country_borders",
  "parentId": "administrative",
  "nameKey": "layer.countryBorders",
  "type": "vector-line",
  "defaultVisible": true,
  "providerCandidates": ["GEOB_OPEN", "OVERTURE_DIV", "OSM_GEOFABRIK"],
  "qualityPolicy": "metadata-and-region-aware",
  "minZoom": 0,
  "maxZoom": 22,
  "nativeCrs": null,
  "attributionRequired": true,
  "deliveryMode": "online-streaming-only",
  "endpointKind": "vector-tiles-from-approved-host",
  "downloadWholeDatasetInClient": false,
  "requestPolicy": "visible-viewport-only",
  "cachePolicy": "obey-provider-http-and-license",
  "status": "not-configured"
}
```

> هذا مثال تصميمي **وليس ملف ضبط جاهز للعمل**. قيم الدقة/CRS/endpoint الفعلية تُجلب من metadata ويجري اختبارها آليًا قبل تمكين مصدر.

### أمثلة روابط API مؤكدة بنيويًا

```text
geoBoundaries Egypt ADM0:
https://www.geoboundaries.org/api/current/gbOpen/EGY/ADM0/

geoBoundaries Egypt ADM1:
https://www.geoboundaries.org/api/current/gbOpen/EGY/ADM1/

Overture docs, dataset-specific paths and release versions:
https://docs.overturemaps.org/guides/divisions/

NASA GIBS standard endpoints and product catalog:
https://nasa-gibs.github.io/gibs-api-docs/access-basics/

Copernicus STAC current documentation (do not use deprecated catalog endpoint):
https://documentation.dataspace.copernicus.eu/APIs/STAC.html
```

**API implementation note:** هذه روابط **فحص metadata ومصادر بيانات**، **وليست خدمة Tiles مباشرة للعميل**. على جانب الخادم فقط: افحص geoBoundaries ثم اقرأ `gjDownloadURL` أو الحقل الفعلي و`boundaryYearRepresented` و`boundarySource` و`boundaryLicense`، وجهّز بلاطات مبنية على الهندسة الأصلية الصحيحة وقدّمها عبر endpoint مستضاف مرخص. لا يُنزّل تطبيق Windows الرابط `gjDownloadURL`. في Overture ثبّت release معلومًا في عمليات تحضير الخادم، بدل افتراض رابط بتاريخ غير معروف.

---

## 7. صور الأقمار، 3D والخرائط التاريخية — اختلافات لا يجب إخفاؤها

### 7.1 3D Buildings

**ثلاثة مستويات مختلفة:**

1. **2D building polygons:** من OSM/Overture/Google Open Buildings/Microsoft. جودة footprint تختلف، ولا تحتوي كلها على ارتفاعات.
2. **2.5D extrusions:** أضف `height` أو `building:levels` عند توافرها؛ إذا وُجد تقدير فقط، ضع metadata يصرح بأنه **Estimated**. ممنوع إعطاء كل مبنى ارتفاعًا افتراضيًا ثم وصف النتائج بأنها واقعية.
3. **Photorealistic meshes:** يحتاج صور طيران/ليدار ونموذج 3D وحقوق إعادة استخدام؛ يمكن إضافة طبقة مجسمات مدينة مفتوحة عند العثور على مصدرها المعتمد، لكنها **ليست تغطية عالمية مجانية مساوية لجوجل**.

**ميزة من Google مجانية فعلًا:** [Google Research Open Buildings](https://sites.research.google/gr/open-buildings/) مشروع **مستقل ومفتوح** يتيح بصمات مبانٍ في إفريقيا، لذا يمكن الاستفادة منه ضمن Z-EARTH بمراعاة الترخيص. لا يتيح صور الأقمار الأصلية التي اشتُقت منها المباني.

### 7.2 Satellite imagery

- **Sentinel-2** مجاني ومفيد جدًا للرصد والتحليل والتواريخ؛ ليس بديلًا عن التصوير التجاري المترّي/دون المتري لكل شوارع ومدن العالم.
- **NASA GIBS** خيار مباشر لطبقات مرئية زمنية عبر WMS/WMTS، لكن ليس ضمانًا لصورة يومية صافية من السحب لكل مكان.
- **Historical Satellite Imagery** يختلف عن **Historical Maps**. اجعل الأول Timeline مع acquisition date، والثاني خرائط مسندة جغرافيًا مع map year / georeferencing metadata / حقوق إعادة النشر.
- لا تصف أي imagery بأنها "Live" ما لم يكن مصدرها وتواترها ودلالتها الزمنية مؤكدًا؛ بعض المصادر near-real-time مؤجلة لساعات.

### 7.3 Terrain & Bathymetry

- ابدأ بكرَة أرضية محلية منخفضة الدقة من Natural Earth + Ellipsoid offline، ثم Copernicus DEM GLO-30 Public عند وجود بلاطات، وGLO-90 كبديل؛ افصل مباني DSM عن التضاريس الأرضية.
- أضف GEBCO_2026 للأعماق، وTID quality ليعرف المستخدم قياس العمق مباشرة أم توقعه. لا تستخدم الأعماق للملاحة البحرية.
- أي vertical exaggeration يؤثر **على العرض فقط** وليس الارتفاعات الحسابية أو القياس الحقيقي.

### 7.4 Historical Maps / Rumsey

- Rumsey مصدر أصلي للخرائط في لقطة المستخدم، وليس مجرد بديل مشابه.
- تتيح المجموعة عرض خرائط تاريخية داخل Google Earth عبر روابط KML وفق شروطها، لكن **ليس ذلك ترخيصًا عامًا لنسخ جميع الخرائط وإعادة تقديمها من خادم Z-EARTH**.
- دعم استيراد KML/GeoTIFF/خرائط مرخصة + 2D opacity slider + swipe compare + date filter، مع rights metadata لكل عنصر.

---

## 8. سياسة مصادر مجانية قابلة للتوزيع

| فئة الاستخدام | القرار |
|---|---|
| بيانات مفتوحة بنص ترخيص واضح (Natural Earth، geoBoundaries gbOpen، Overture حسب theme، OSM/Geofabrik، Google Open Buildings، Microsoft current repo) | يمكن استخدامها إذا التزمنا بالـ attribution والاشتراطات الخاصة بمشتقات قواعد البيانات ونشرها |
| Tile service عام مجاني (OSMF) | عرض تفاعلي منضبط فقط وفق السياسة؛ لا prefetch، لا تصنيع offline archive منها، ولا ضمان إنتاجي |
| API مجاني غير تجاري (مثل Open-Meteo public tier) | لا تجعله خدمة إلزامية في تطبيق عام/تجاري بلا ترخيص ملائم؛ اترك خيار API أو مصدر آخر |
| مجموعة صور/خرائط فردية مع شروط عناصر مختلفة (Wikimedia/Rumsey/LOC) | افحص الترخيص **لكل صورة**؛ التضمين أو إعادة النشر ليس حقًا تلقائيًا |
| WDPA/GADM | لا توزع البيانات مسبقًا في البرنامج دون إذن مناسب؛ الاختيارية عبر مستخدم مرخّص فقط |
| بيانات Google Earth/Maps/3D المدفوعة أو المحمية | ممنوع scrapes/tiles extraction؛ لا تستعملها لبناء منتج مماثل دون ترخيص رسمي صريح |

**معلومة قانونية وتشغيلية مهمة:** «Open-source application» لا يجعل كل استعمال للمزود «غير تجاري» تلقائيًا. افصل ترخيص برنامج Z-EARTH عن ترخيص البيانات والـAPI. راجع التراخيص عند كل إصدار.

---

## 9. خطة تطوير مرحلية — مكون واحد في كل مرة

تنفيذ متسق مع أسلوب المشروع: تغيير واحد مركز في كل مرحلة، ثم ملف تقرير في `Development Reports`، لا دمج متسرع لكل المكونات دفعة واحدة.

### Sprint L1 — Borders & Labels (الأولوية P0)

- Audit كامل لطبقة Layers والشجرة ومحرك Cesium الحالي، وتحديد الـCommands الموجودة والـstubs قبل الكتابة.
- بناء Layer Catalog وProvider Registry وLayer Properties وcheckbox مربوط بحالة الرسم.
- دمج **geoBoundaries/Overture** بوصفها **مصادر تحضير على الخادم فقط**؛ فحص خدمة Tiles مستضافة لمصر ADM0/ADM1، ومنع تحميل ملفات المصدر إلى أجهزة المستخدم. عند عدم وجود خادم/endpoint مرخّص، سجل العائق ولا تعرض الطبقة وكأنها تعمل.
- نظام LOD ووثيقة جودة واختيار المصدر والـattribution، واختبارات التفصيل والسواحل والحدود المتنازع عليها.
- تقرير `Development Reports/Layers_01_Borders_Labels_Report.md` يحوي نتائج فعليّة وصور واختبارات وأعطال.

### Sprint L2 — Roads, Places, Labels

- Overture Transportation/Places + OSM كمصادر تحضير على الخادم؛ العميل يستقبل Vector Tiles فقط عند الحاجة، مع style حسب road class وdecluttering للتسميات العربية/الإنجليزية.
- تقرير `Layers_02_Transportation_Places_Report.md`.

### Sprint L3 — Buildings 2D/2.5D

- Overture/Google Open Buildings/Microsoft، ارتفاعات واقعية عند توافرها، بيانات confidence، واختبار مدينة في مصر ومدينة أخرى مختلفة.
- تقرير `Layers_03_Buildings_Report.md`.

### Sprint L4 — Water / Parks / Government Layers

- OSM + مصادر حكومية مرخصة + تنسيق تلقائي؛ لا حصر للخرائط الأمريكية فقط.
- تقرير `Layers_04_Nature_Hydro_Government_Report.md`.

### Sprint L5 — Images, Photos, Weather

- NASA GIBS / Commons / Panoramax / Weather optional licensing، مع اختبارات timestamp وحقوق الصور.
- تقرير `Layers_05_Imagery_Weather_Photos_Report.md`.

### Sprint L6 — Historical Maps & Timeline

- Library of Congress، عناصر Rumsey المرخصة، فهرس تواريخ/extent + مقارنات زمنية وتمييز أنواع التواريخ.
- تقرير `Layers_06_Historical_Maps_Report.md`.

### Sprint L7 — QA / Streaming / Distribution

- اختبارات الذاكرة والأداء واستهلاك الإنترنت وViewport-only requests وإلغاء التحميل، الانتقال 2D/3D/بدون إنترنت، والتراخيص؛ لا تنزيل ملفات قواعد بيانات أو Offline Packs في التطبيق الافتراضي.
- تقرير `Layers_07_Release_Acceptance_Report.md` وتحديث `Development Reports/INDEX.md`.

### معايير قبول المرحلة الأولى

- [ ] تظهر شجرة Layers مترابطة وتدعم التوسيع والطي وإظهار الحالة الجزئية للأب.
- [ ] إيقاف Country Borders يجعل **كل** خطوط طبقتها تختفي فورًا في 2D و3D، وإعادة التشغيل تحترم اختيار المستخدم.
- [ ] تعطيل Labels لا يزيل Border والعكس؛ لا توجد خطوط أو أيقونات عالقة عند disable.
- [ ] يمكن عرض حدود دولة كاملة على Globe، ثم التحرك حتى مستوى تفصيل محلي دون إظهار بيانات معممة كأنها دقيقة.
- [ ] يظهر مصدر وسنة التمثيل وترخيص كل طبقة وتاريخ تحديث الخدمة؛ لا يوجد `accuracy_m` مفترض.
- [ ] تفحص طبقات ADM0 وADM1 لمصر + دولة أوروبية ذات بيانات رسمية متاحة + دولة لها سواحل/جزر معقدة؛ تُوثّق فروقها.
- [ ] لا تعطل تغييرات الطبقات Grid أو Sunlight أو Terrain أو My Places أو Camera أو أدوات القياس.
- [ ] إن توفّر Globe أساسي مرخّص ضمن حجم التثبيت فهو يظهر دون اتصال؛ جميع الطبقات المتدفقة تحتاج الشبكة أو cache مسموحًا وتعرض حالة Offline بوضوح.
- [ ] لا يُستخدم OSM public tiles للاستباقية أو التنزيل الجماعي، ولا تُنسخ خرائط Google Earth.
- [ ] تقرير اختبار فعلي شامل: Build Pass/Fail، UI screenshots، provider HTTP tests، نتائج LOD/topology، وظائف 2D/3D، حقوق المصادر. لا تكتب PASS لشيء لم يُختبر.

---

## 10. أمر مختصر للوكيل لتنفيذ المرحلة الأولى من هذا المستند

> اقرأ `ZEARTH_Layers_Open_Data_Implementation_Guide_AR.md` وقارن المواصفة مع كود Z-EARTH Desktop الفعلي والمهارات الموجودة وخطة المشروع. نفذ **Sprint L1 فقط: Borders & Labels + Layer Catalog/Provider Registry + Quality QA** مع الحفاظ على تصميم الشجرة ومسارات الأوامر والمحرك. **التزم بأن تكون الطبقات Online Streaming عند العرض فقط، ومنع أي Download كامل لحدود الدول/المحافظات أو MBTiles أو ملفات PBF إلى جهاز المستخدم، وإن احتاجت geoBoundaries/Overture تجهيزًا يتم على خادم مستقل.** لا تستخدم بيانات Google Earth المحمية، ولا تعتمد على Natural Earth كحدود دقيقة عند التكبير. اربط checkbox بالرسم الحقيقي في Cesium وطبّق fallback وتوثيق مصدر/سنة/ترخيص الطبقة واستهلاك الشبكة الخاص بها. اختبر على الأقل مصر وأماكن مرجعية أخرى، وسجّل نتائج الاختبارات الحقيقية مع لقطات الواجهة وأخطاء الاتصال في `Development Reports/Layers_01_Borders_Labels_Report.md`. لا تنتقل للمرحلة التالية قبل توثيق نتائج المرحلة الأولى؛ وإذا تعذر أي مورد أو ترخيص، صرّح بذلك ولا تعرض طبقة وهمية باعتبارها عاملة.

---

## 11. مراجع أولية مباشرة (للتدقيق والتحديث)

**Google Earth ومصادره وقيوده**
- [Google Earth Pro — Choose layers](https://support.google.com/earth/answer/148130?hl=en)
- [Google Earth — Learn about data layers / metadata / export restrictions](https://developers.google.com/maps/documentation/earth/learn-about-data-layers)
- [Google Earth — Available data layers and source fields](https://developers.google.com/maps/documentation/earth/available-data-layers)
- [Google Geo Guidelines — 3D reconstruction restrictions](https://about.google/brand-resource-center/products-and-services/geo-guidelines/)
- [Google Earth — How images are collected](https://support.google.com/earth/answer/6327779/how-images-are-collected?hl=en)
- [Google Photos layer / Panoramio retired](https://www.google.com/earth/rpc/legacyphotos?hl=en-US&ui=legacy)

**حدود وطرق ومبانٍ**
- [geoBoundaries](https://www.geoboundaries.org/api.html)
- [geoBoundaries Global vs Individual Country](https://www.geoboundaries.org/globalDownloads.html)
- [Overture divisions](https://docs.overturemaps.org/guides/divisions/)
- [Overture attribution](https://docs.overturemaps.org/attribution/)
- [Overture buildings](https://docs.overturemaps.org/guides/buildings/)
- [Overture places](https://docs.overturemaps.org/guides/places/)
- [Geofabrik](https://www.geofabrik.de/data/download.html)
- [Natural Earth](https://nacis.org/initiatives/natural-earth/)
- [Google Open Buildings](https://sites.research.google/gr/open-buildings/)
- [Microsoft Global ML Building Footprints](https://github.com/microsoft/GlobalMLBuildingFootprints)
- [OSMF tiles](https://operations.osmfoundation.org/policies/tiles/)
- [Nominatim usage](https://operations.osmfoundation.org/policies/nominatim/)

**صور وارتفاعات وخرائط تاريخية**
- [NASA GIBS](https://nasa-gibs.github.io/gibs-api-docs/access-basics/)
- [Open-Meteo](https://github.com/open-meteo/open-meteo)
- [Copernicus DEM AWS](https://registry.opendata.aws/copernicus-dem/)
- [GEBCO_2026](https://www.gebco.net/data-products-gridded-bathymetry-data/gebco2026-grid)
- [Copernicus STAC](https://documentation.dataspace.copernicus.eu/APIs/STAC.html)
- [MediaWiki GeoSearch](https://www.mediawiki.org/wiki/API:Geosearch)
- [Panoramax API](https://docs.panoramax.fr/backend/api/api/)
- [David Rumsey Google Earth historical maps](https://www.davidrumsey.com/view/google-earth)
- [David Rumsey permissions](https://www.davidrumsey.com/about/copyright-and-permissions)
- [Library of Congress APIs](https://www.loc.gov/apis/)
- [USGS National Map](https://www.usgs.gov/tools/download-data-maps-national-map)
- [Eurostat GISCO](https://ec.europa.eu/eurostat/web/gisco/geodata/)
- [PMTiles Cloud Storage + HTTP Range](https://docs.protomaps.com/pmtiles/cloud-storage)
- [MapLibre Vector Tile Sources](https://maplibre.org/maplibre-gl-js/docs/API/classes/VectorTileSource/)
- [NASA GIBS Streaming WMTS/WMS/XYZ](https://nasa-gibs.github.io/gibs-api-docs/access-basics/)
- [OSM Tile Usage Policy — strictly no prefetch](https://operations.osmfoundation.org/policies/tiles/)

---

**ملاحظة ختامية:** الأسماء والإصدارات والرخص والخدمات قد تتغير؛ تحقق منها مجددًا أثناء مرحلة التنفيذ. هذا المستند مبني على المصادر المنشورة حتى تاريخ كتابته وعلى الخطة المضغوطة المرفقة للمشروع؛ **لم يتم هنا تشغيل Z-EARTH أو اختبار اتصالات جميع APIs أو تعديل الفرع الرئيسي**.


## 12. ملحوظة إصدار 2 — الفرق بين البث والتنزيل

**القاعدة النهائية للمطور:** إذا كانت طبقة من المصادر أعلاه لا تعطي Tiles مباشرة، فهذا **لا** يجيز للبرنامج تنزيل ملفات الدولة من API في الخلفية، بل يتطلب endpoint بث قانونيًا (مزود خارجي أو استضافة Z-EARTH) يُبنى مسبقًا من مصدر معتمد. وإن لم يتوافر endpoint فلا تُفعّل الطبقة على أنها جاهزة. قد يخزّن عميل الخرائط قطعًا صغيرة وفق سياسات HTTP؛ ذلك لا يعادل تنزيل dataset كامل. يجب توضيح الحجم المنقول والإسناد والترخيص وقيود الحصص.
