#!/usr/bin/env python3
"""Validate locally vendored GIS skills and Windows app file shapes, no extra deps."""
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SKILLS = ROOT / ".github" / "skills"
CORE = (
    "gis-development", "gis-desktop-application-development",
    "geospatial-web-mapping", "coordinate-reference-systems",
    "geospatial-data-processing", "geospatial-api-integration",
    "kml-kmz-engineering", "remote-sensing-pipelines",
)
errors = []
for skill in CORE:
    doc = SKILLS / skill / "SKILL.md"
    if not doc.is_file():
        errors.append("Missing core imported skill: " + str(doc.relative_to(ROOT)))

for doc in SKILLS.glob("*/SKILL.md"):
    text = doc.read_text(encoding="utf-8")
    if not text.startswith("---\n"):
        errors.append("Missing YAML frontmatter: " + str(doc.relative_to(ROOT)))
    if doc.parent.name in CORE and "name: " + doc.parent.name not in text:
        errors.append("Invalid skill name: " + str(doc.relative_to(ROOT)))
    # Imported generic skill docs must resolve their direct relative reference links.
    if doc.parent.name in CORE or doc.parent.name in {
        "arcgis-toolbox-engineering", "qgis-plugin-development",
        "global-mapper-development", "terrain-hydrology-lidar",
        "spatial-databases", "geospatial-ai-agents",
        "geospatial-data-catalogs", "geospatial-data-discovery",
    }:
        for link in re.findall(r"\]\(([^)]+)\)", text):
            link = link.split("#", 1)[0]
            if link.startswith("../") and not (doc.parent / link).resolve().is_file():
                errors.append("{} broken local skill link: {}".format(doc.relative_to(ROOT), link))

for doc in (ROOT / "src").rglob("*.xaml"):
    try:
        ET.parse(doc)
    except ET.ParseError as exc:
        errors.append("Invalid XML/XAML {}: {}".format(doc.relative_to(ROOT), exc))

for expected in ("AGENTS.md", "README.md", "README.ar.md", "docs/GIS_SKILLS_INTEGRATION.md"):
    if not (ROOT / expected).is_file():
        errors.append("Missing fresh-checkout guide: " + expected)

if errors:
    print("\n".join("ERROR: " + x for x in errors))
    sys.exit(1)
print("Skill and XML inspection passed; does NOT validate WPF runtime bindings or WebView2.")
