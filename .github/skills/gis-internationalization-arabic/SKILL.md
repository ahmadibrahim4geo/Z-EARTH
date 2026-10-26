---
name: gis-internationalization-arabic
description: Design and test multilingual GIS applications and datasets, with rigorous Arabic Unicode, bidirectional layout, search, labeling, data interchange and accessibility support.
license: MIT
---

# GIS Internationalization, Localization & Arabic Engineering

**Free-first policy:** Follow [free-first-policy.md](../../references/free-first-policy.md). Disclose commercial runtime and library dependencies.

**First read:** [Plain-language concepts, examples and upstream references](../../references/gis-multilingual-arabic-guide.md). Read the guide before designing multilingual schemas, map labels or Arabic UI.

Load this skill for GIS UI, cartography, spatial attributes, geocoding, exports, or toolboxes that accept or display multilingual text. Arabic is a first-class test target; French, English and other languages use the same extensible locale architecture.

## Language architecture
- Separate stable internal identifiers (field names, API keys, file names and source code symbols) from localized display names, field aliases, messages and help text. Preserve original user-entered Arabic attribute values; do not transliterate or translate them without explicit consent.
- Store locale resources in UTF-8 and use language tags (e.g. ar-EG, en, fr). Provide fallback per missing translation key, plural rules, date/number formatting, localized errors, and language-independent data schemas.
- Keep localized labels separate from algorithmic parameters and persistence. Switching languages must not modify spatial geometry or data.

## Arabic rendering and bidirectional text
- Use Unicode-capable rendering with Arabic shaping, BiDi and font fallback. UTF-8 alone does not guarantee proper rendering.
- In HTML set lang and dir at document/component level; isolate embedded Latin code, coordinates, file paths, dates and identifiers with bdi/dir=auto or appropriate directional isolates. Never reverse strings manually.
- In Qt enable appropriate layout direction and test complex-text shaping; in WPF test FlowDirection, text runs and fallback fonts; for GIS map renderers verify actual Arabic glyph shaping and label placement rather than assuming browser behavior.
- Test punctuation, parentheses, mixed Arabic/English, negative values, decimal separators, numerals, combining marks, diacritics, Kashida and long labels. Do not distribute proprietary fonts without rights.
- Respect RTL in navigation, forms, tooltips, keyboard focus, icons that convey direction, legends and layout export; geographic east/west and coordinate axes must remain semantically correct.

## GIS attributes and storage
- Prefer GeoPackage/PostGIS/UTF-8 GeoJSON for rich Unicode attributes; verify driver and client encodings.
- Shapefile DBF has encoding and field-name limitations; test .cpg and DBF encoding explicitly, prefer ASCII-compatible field identifiers with Arabic aliases. Never claim universal Unicode interoperability for Shapefile.
- CSV: specify UTF-8 and document Excel import/UTF-8 BOM needs where applicable. XLSX: use Unicode strings and test Excel round-trip.
- KML/KMZ: preserve UTF-8 text in names, descriptions, ExtendedData, folder hierarchy and CDATA/escaped markup. Ensure XML escaping and prohibit unsafe HTML or script injection. Reopen in target hosts.
- ArcGIS Pro/ArcMap and QGIS: verify field aliases, tool parameter labels, layer names, attribute tables, layouts, legends and exported maps in actual host versions.

## Search, sorting and normalization
- Preserve original text; normalize only a separate search index when useful. Apply Unicode normalization carefully and explicitly document search-only folding.
- Optional Arabic search folding may handle alef/hamza variants, ya/alef maqsura, tatweel and diacritics; avoid treating taa marbuta/haa as equivalent without domain-specific justification.
- Use locale-aware collation where available. Test Arabic, French accents, mixed scripts, and missing values. Never strip diacritics from stored authoritative place names automatically.
- Geocoding: keep multilingual aliases and alternate names with provenance and locale tags; return original authoritative labels.

## Safety and correctness
- Escape localized UI text for its output context (HTML, XML, SQL parameters, JSON). Never concatenate translated strings into executable code or SQL.
- Test for spoofing via invisible direction controls in file names and untrusted labels; preserve legitimate multilingual data while isolating rendering and warning on suspicious identifiers.
- Verify long translated strings, font fallback, offline operation, accessibility screen readers and keyboard navigation.

## Acceptance matrix
- [ ] UI switches between Arabic RTL, English LTR and a third language without missing keys.
- [ ] Arabic letters join correctly in UI, map labels, tables, PDF and exports.
- [ ] Mixed Arabic/Latin text, coordinates and parentheses render in correct visual order.
- [ ] GeoPackage, GeoJSON, XLSX and KML/KMZ preserve Arabic attributes after round-trip.
- [ ] Shapefile limitations and host-specific behavior documented.
- [ ] Search finds intended Arabic variants without modifying source values.
- [ ] Locale switching leaves CRS, geometry, measurements and saved identifiers unchanged.
- [ ] Performance and crash-prevention checks cover long and malformed Unicode strings.
- [ ] Native host and font-rendering claims have real screenshots/acceptance evidence.

See [reliability guide](../../docs/PERFORMANCE_RELIABILITY.md), [KML/KMZ skill](../kml-kmz-engineering/SKILL.md), and [quality benchmark](../../docs/QUALITY_BENCHMARK.md).
