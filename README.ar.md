# Z EARTH — برنامج GIS مكتبي وكرة أرضية ثلاثية الأبعاد

مشروع لنظام **Windows** باستخدام **.NET 10 وWPF وCesiumJS داخل WebView2**، مع GDAL/PROJ وSQLite ودعم KML/KMZ.

**المطور:** Ahmed Ibrahim. [English README](README.md) · [ملف تعليمات الـAgent](AGENTS.md) · [دليل المستخدم](docs/USER_MANUAL_AR.md).

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

أضفنا **16 مهارة GIS مثبتة النسخة** داخل `.github/skills/` جنبًا إلى جنب مع مهارات CesiumJS وZ EARTH الموجودة بالفعل. يقرأ الـAgent [AGENTS.md](AGENTS.md) أولًا، ثم مهارة `gis-development` الأساسية، ثم مهارات التخصص الضرورية حسب المشكلة.

الهدف إن الـAgent يحدد السبب، ويصلح الكود، ويضيف اختبارًا، وبعدها يتحقق من التشغيل الحقيقي دون الادعاء بنجاح لم يختبره. [شرح الربط والمصادر](docs/GIS_SKILLS_INTEGRATION.md) و[قائمة الاختبار العملي](docs/FRESH_CLONE_VALIDATION.md).

## المجانية والتراخيص

الكود وأدوات المعالجة العامة تعتمد على حلول مجانية أو مفتوحة المصدر كلما أمكن، دون إلزام بشراء ArcGIS أو Global Mapper. **الخدمات الخارجية** مثل مصادر الصور والخرائط والتضاريس قد تخضع لحصص مجانية أو شروط استخدام وحقوق إعادة توزيع مستقلة. لا تحفظ مفاتيح الخدمات الحساسة في المستودع أو تشاركها مع الآخرين.

**إصدار الملفات ليس شهادة بأن البرنامج خالٍ من الأخطاء.** كل إصلاح يحتاج اختبارات ومراجعة تشغيل Windows قبل اعتماده.
