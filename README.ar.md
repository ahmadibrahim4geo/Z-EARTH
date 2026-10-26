<p align="center">
  <img src="assets/branding/zearth-repo-header.webp" alt="Z EARTH Desktop GIS — official repository banner with blue Z globe emblem and topographic background" width="720" />
</p>

# Z EARTH — برنامج GIS مكتبي وكرة أرضية ثلاثية الأبعاد

مشروع لنظام **Windows** باستخدام **.NET 10 وWPF وCesiumJS داخل WebView2**، مع GDAL/PROJ وSQLite ودعم KML/KMZ.

**المطور:** Ahmed Ibrahim. [English README](README.md) · [ملف تعليمات الـAgent](AGENTS.md) · [دليل المستخدم](docs/USER_MANUAL_AR.md).

## ابدأ من هنا — نسخة تطوير تجريبية

| المطلوب | الرابط |
| --- | --- |
| تحميل النسخة المكتبية التجريبية | [طريقة التنزيل](docs/PORTABLE_PREVIEW.md) · [نتائج GitHub Actions](https://github.com/ahmadibrahim4geo/Z-EARTH/actions/workflows/build-and-test.yml) |
| معرفة الوظائف الحالية ومستوى اختبارها | [دليل البدء](docs/REPOSITORY_ONBOARDING.md) · [مصفوفة المقارنة](docs/GOOGLE_EARTH_PRO_PARITY.md) |
| الإبلاغ عن خطأ أو المساهمة | [فتح بلاغ](https://github.com/ahmadibrahim4geo/Z-EARTH/issues/new/choose) · [دليل المساهمة](CONTRIBUTING.md) |
| الأمان وترخيص المصدر | [حالة الترخيص والمراجعة المطلوبة](docs/LICENSE_STATUS.md) · [سياسة الأمان](SECURITY.md) |

**حالة المشروع:** البرنامج لا يزال قيد التطوير؛ نجاح GitHub Actions لا يثبت اكتمال تشغيل واجهة Windows وWebView2. كما يلزم اعتماد ترخيص صريح لكود المشروع بعد فحص تراخيص المكونات والبيانات الخارجية.

## تحميل Z EARTH.exe لتجربة البرنامج

لم يكن المستودع ينشر ملف EXE في قسم Releases رغم نجاح بناء الكود. أضفنا آلية تجهيز نسخة **Windows x64 Portable Preview** قابلة للتنزيل من GitHub Actions بعد اجتياز اختبارات المشروع. افتح [صفحة البناء والتحميل](https://github.com/ahmadibrahim4geo/Z-EARTH/actions/workflows/build-and-test.yml)، واختر أحدث تشغيل ناجح على فرع main، ثم حمّل `Z-EARTH-v2.2.4-win-x64-portable-preview` من قسم Artifacts. بعد فك الضغط ستجد `Z EARTH.exe` مع ملفات تشغيل Cesium؛ **لا تنقل EXE بمفرده**.

اقرأ [دليل تنزيل وتشغيل النسخة التجريبية](docs/PORTABLE_PREVIEW.md). النسخة غير موقعة رقميًا وليست إصدارًا نهائيًا؛ يلزم WebView2 وWindows x64، وما زالت اختبارات تشغيل الكارت الرسومي والخدمات الحية على جهاز Windows مطلوبة. ملفات v1.0.0 القديمة لا تعبر عن إصدار كود 2.2.4 الحالي.

## طريقة البدء من الصفر

1. نزّل المشروع من [GitHub](https://github.com/ahmadibrahim4geo/Z-EARTH) على جهاز Windows 10/11 بنظام x64.
2. ثبّت **.NET 10 SDK** و**Microsoft Edge WebView2 Runtime**؛ ويفضل وجود بطاقة رسوميات تدعم WebGL.
3. افتح PowerShell من مجلد المشروع، ثم شغّل:

```powershell
dotnet restore ZEarth.sln
dotnet build ZEarth.sln -c Release --no-restore
dotnet test tests/ZEarth.Tests/ZEarth.Tests.csproj -c Release --no-build
.\scripts\build-and-test.ps1
```

بعد نجاح البناء والاختبارات، افتح `dist/Z EARTH-Preview/Z EARTH.exe`. يظل الاختبار الفعلي على Windows ضروريًا للتأكد من عمل الخرائط، والإضاءة الشمسية، والمحيط الجوي، والتضاريس، والحفظ وفتح KML؛ **نجاح الاختبارات الوهمية لا يثبت عمل محرك Cesium فعليًا**.

## استخدام مهارات GIS Development Skills

أضفنا **21 مهارة GIS مثبتة النسخة** و**54 ملفًا مرجعيًا** داخل `.github/skills/` جنبًا إلى جنب مع مهارات CesiumJS وZ EARTH الموجودة بالفعل. يقرأ الـAgent [AGENTS.md](AGENTS.md) أولًا، ثم مهارة `gis-development` الأساسية، ثم مهارات التخصص الضرورية حسب المشكلة.

تم تثبيت إصدار المصدر برقم Commit محدد وتسجيل بصمات الملفات في [سجل المزامنة](docs/GIS_SKILLS_SNAPSHOT.json)، ويمكن التأكد منها بالأمر `python scripts/validate-agent-skills.py`. وأضفنا [مهارة مخصصة لوظائف Google Earth Pro](.github/skills/zearth-google-earth-pro-workflows/SKILL.md)، مرتبطة بـ[الصور التاريخية](.github/references/historical-imagery-timeline.md)، و[KML/KMZ](.github/references/google-kml-kmz-compatibility.md)، و[World Gazetteer](.github/references/world-gazetteer-reference-layer.md)، إلى جانب مهارة الأمان. **دي تعليمات للـAgent اللي بيطوّر البرنامج، ومش معناها إن التطبيق المثبت بيقرأ SKILL.md أثناء تشغيله**.

أضفنا مهارتين جديدتين: **Prompt Intent Orchestrator** لتنظيم ومراجعة طلبات التطوير وخططها، و**Agent Development Journal** لتسجيل خطوات التنفيذ والاختبارات والتعديلات في سجل محلي مرقم ومحمي من النشر العام. توجد أدوات Python واختبارات فعلية تحت `.github/scripts` و`.github/tests`. يشرح [دليل تشغيل الـAgent](docs/ZEARTH_AGENT_WORKFLOW.md) الإجراءات. تم تثبيت الملفات من [PR #20 المفتوح في المستودع الأصلي](https://github.com/ahmadibrahim4geo/GIS-Development-Skills/pull/20)، وليس من نسخة نهائية مدمجة. المهارات لا تشغّل نفسها تلقائيًا داخل البرنامج.

الهدف إن الـAgent يحدد السبب، ويصلح الكود، ويضيف اختبارًا، وبعدها يتحقق من التشغيل الحقيقي دون الادعاء بنجاح لم يختبره. [شرح الربط والمصادر](docs/GIS_SKILLS_INTEGRATION.md) و[قائمة الاختبار العملي](docs/FRESH_CLONE_VALIDATION.md).

## التأهيل ليصبح بديلًا لبرنامج Google Earth Pro

بدأنا تطبيق [مصفوفة مقارنة وظيفية تفصيلية](docs/GOOGLE_EARTH_PRO_PARITY.md) تعتمد على وثائق Google Earth Pro الرسمية. التعديلات الأولى تشمل: تحريك مشاهد Saved Scenes لأعلى ولأسفل (أول مشهد هو مشهد بداية المشروع)، إدراج ملفات KML/KMZ وShapefile وGeoJSON وGeoPackage في **Temporary Places** أولًا قبل نقلها اختياريًا إلى My Places، إضافة رموز مميزة لأنواع الـBasemap، وتشغيل خريطة محلية افتراضيًا قبل أي طلب شبكة.

كما أضفنا رسم **Rectangle** بتحديد ركنين مع معاينة شكلية واختبارات حساب الإحداثيات؛ تُرفض المستطيلات العابرة لخط التاريخ الدولي في هذه المرحلة بدلًا من رسمها خطأً. أصبحت نقاط القياس مرقمة داخل دوائر واضحة، ويمكن تحريك لوحة القياس من شريط عنوانها.

**الدقة العلمية:** أزلنا الارتفاعات الاصطناعية من نافذة القطاع التضاريسي. العرض الحالي يعكس منسوب رأس الخط المخزن فقط، وليس عينات DEM الفعلية. ما زال إنشاء قطاع تضاريسي متصل بنموذج الارتفاعات المختار، والتحكم في نافذة القطاع داخل مساحة العمل، وتطوير المقياس والعناصر الرمزية، من المهام قيد التنفيذ وغير معتمدة للاستخدام العلمي بعد.

**أداة REA:** المستودع المرجعي الصحيح هو [morluto/rea](https://github.com/morluto/rea)، وهو برنامج MIT لتحليل سلوك التطبيقات. نسخة المطور المحلية داخل `D:\\My Software\\Zearth\\rea` مستقلة عن Z EARTH. أضفنا [مهارة مخصصة لاستخدامها](.github/skills/zearth-rea-parity/SKILL.md) بعد تحقق الـAgent من الإعداد المحلي، من دون إعادة التنزيل أو نسخ برنامج Google Earth الأصلي. تمت مراجعة توثيق REA على GitHub، لكن لم نحلل برنامج Google Earth Pro المثبت على جهاز المطور عمليًا في هذه المحادثة.

## شريط الصور التاريخية داخل الخريطة

أصبح زر **Historical Imagery → Show Timeline** يفتح **شريطًا صغيرًا داخل مساحة الكرة الأرضية** بدل نافذة Windows تحجب الخريطة. يمكنك التحرك عبر الإصدارات السابقة والتالية، وأقدم وأحدث إصدار، وسحب مؤشر الزمن، واختيار سنة أو تاريخ محدد. إذا لم يوجد إصدار في اليوم المختار، ينتقل إلى أقرب إصدار متاح في ذلك اليوم أو قبله، مع رسالة توضح الاختيار. تستطيع التحريك والتكبير أثناء بقاء الشريط ظاهرًا، وتغلقه بزر × أو Escape لاستعادة خريطة الأساس السابقة.

المصدر هنا **Esri World Imagery Wayback**، وليس الأرشيف الأصلي الخاص بـGoogle Earth Pro. **التاريخ هو تاريخ إصدار النسخة المؤرشفة من الخريطة، وليس بالضرورة تاريخ التقاط الصورة الفضائية في الموقع**. تختلف التحديثات حسب المنطقة والتكبير. يقرأ التطبيق فهرس Esri الحقيقي من الإنترنت؛ لا يصنع تواريخ بديلة عند فشل الاتصال. واجهة الشريط نفسها مجانية، لكن الخدمة والصور تخضع لشروط Esri المستقلة وذكر المصدر. [تفاصيل التنفيذ والاختبارات](docs/HISTORICAL_IMAGERY_TIMELINE.md).
## المجانية والتراخيص

الكود وأدوات المعالجة العامة تعتمد على حلول مجانية أو مفتوحة المصدر كلما أمكن، دون إلزام بشراء ArcGIS أو Global Mapper. **الخدمات الخارجية** مثل مصادر الصور والخرائط والتضاريس قد تخضع لحصص مجانية أو شروط استخدام وحقوق إعادة توزيع مستقلة. لا تحفظ مفاتيح الخدمات الحساسة في المستودع أو تشاركها مع الآخرين.

**إصدار الملفات ليس شهادة بأن البرنامج خالٍ من الأخطاء.** كل إصلاح يحتاج اختبارات ومراجعة تشغيل Windows قبل اعتماده.
