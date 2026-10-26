# Security policy — Z EARTH / سياسة الأمان

## Private reporting

Please report potentially exploitable issues in KMZ/ZIP/XML parsing, KML references, WebView2 message bridging, script execution, local file access, network providers, credentials or sensitive location handling **privately** using GitHub's vulnerability-reporting feature if enabled, or by emailing **ahmadibrahim.geo@gmail.com**.

Do not post working exploits or include private location traces, customer shapefiles, real API tokens, exported databases or other users' data in public issues. Sanitize stack traces and local paths; do not test against nonconsenting services.

## Supported status and verification

The repository is an experimental Windows development project. A test passing in CI is not evidence that every WebView2/GPU, network, consent, geospatial or archive-security boundary holds in native execution. Reports are triaged as maintainer capacity permits; there is no guaranteed remediation SLA.

Review the [licensing/privacy notices](LEGAL_AND_PRIVACY_NOTICES.md), [onboarding](docs/REPOSITORY_ONBOARDING.md) and current [README limitations](README.md). Avoid blanket claims that vulnerability classes are fully prevented without negative tests and reproducible T3/T4 evidence.

**بالعربية:** بلّغ عن الثغرات بصورة خاصة، ولا تنشر بيانات المستخدمين أو المواقع الدقيقة أو المفاتيح أو ملفات KML/KMZ التي تحتوي على معلومات سرية.
