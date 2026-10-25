#!/usr/bin/env python3
"""Offline integrity audit of pinned GIS Agent Skills and Z EARTH's XAML; no network."""
import hashlib
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SKILLS = ROOT / ".github" / "skills"
SNAPSHOT = ROOT / "docs" / "GIS_SKILLS_SNAPSHOT.json"
ADAPTER = SKILLS / "zearth-google-earth-pro-workflows" / "SKILL.md"
PINNED_SKILL_COUNT = 21
PINNED_REFERENCE_COUNT = 54


def git_blob_sha(path: Path) -> str:
    raw = path.read_bytes()
    head = ("blob " + str(len(raw)) + "\0").encode("ascii")
    return hashlib.sha1(head + raw).hexdigest()


def links_in(text: str):
    # Markdown local file links. Remote links and fragment-only anchors are not local files.
    for link in re.findall(r"\]\(([^)]+)\)", text):
        target = link.split("#", 1)[0].strip()
        if not target or target.startswith(("#", "//")):
            continue
        if re.match(r"^[A-Za-z][A-Za-z0-9+.-]*:", target):
            continue
        # Paths with query strings are not supported for local skills.
        yield target.replace("%20", " ")


def main():
    errors = []
    if not SNAPSHOT.is_file():
        errors.append("Missing pinned GIS snapshot manifest")
        manifest = {}
    else:
        try:
            manifest = json.loads(SNAPSHOT.read_text(encoding="utf-8"))
        except (ValueError, OSError) as exc:
            errors.append("Invalid snapshot manifest: " + str(exc))
            manifest = {}

    rows = manifest.get("files", [])
    if manifest.get("skill_count") != PINNED_SKILL_COUNT:
        errors.append(f"Expected {PINNED_SKILL_COUNT} pinned upstream GIS skills")
    if manifest.get("reference_count") != PINNED_REFERENCE_COUNT:
        errors.append(f"Expected {PINNED_REFERENCE_COUNT} pinned GIS reference/catalog files")
    if len(rows) != PINNED_SKILL_COUNT + PINNED_REFERENCE_COUNT:
        errors.append("Unexpected pinned snapshot inventory size")
    if not re.fullmatch(r"[a-f0-9]{40}", manifest.get("source_commit", "")):
        errors.append("Invalid upstream source commit in manifest")
    if manifest.get("source_repository") != "ahmadibrahim4geo/GIS-Development-Skills":
        errors.append("Unexpected skill snapshot origin")

    seen = set()
    pinned = []
    for row in rows:
        path_text = row.get("path", "") if isinstance(row, dict) else ""
        digest = row.get("git_blob_sha", "") if isinstance(row, dict) else ""
        rel = Path(path_text)
        skill = bool(re.fullmatch(r"\.github/skills/[^/]+/SKILL\.md", rel.as_posix()))
        ref = rel.as_posix().startswith(".github/references/")
        if not (skill or ref) or path_text in seen:
            errors.append("Invalid/duplicate snapshot entry: " + repr(path_text))
            continue
        seen.add(path_text)
        full = (ROOT / rel).resolve()
        if not full.is_relative_to(ROOT) or not full.is_file():
            errors.append("Missing or unsafe pinned file: " + path_text)
            continue
        if not re.fullmatch(r"[a-f0-9]{40}", digest) or git_blob_sha(full) != digest:
            errors.append("Pinned source checksum mismatch: " + path_text)
        if skill:
            pinned.append(full)

    for path in pinned + [ADAPTER]:
        if not path.is_file():
            errors.append("Missing source or Z EARTH routing skill: " + str(path))
            continue
        text = path.read_text(encoding="utf-8")
        if not text.startswith("---\n"):
            errors.append("Missing YAML frontmatter: " + str(path.relative_to(ROOT)))
        if not re.search(r"(?m)^name:\s*" + re.escape(path.parent.name) + r"\s*$", text):
            errors.append("Wrong skill name: " + str(path.relative_to(ROOT)))
        for link in links_in(text):
            target = (path.parent / link).resolve()
            if not target.is_relative_to(ROOT) or not target.is_file():
                errors.append("Broken skill link: {} -> {}".format(path.relative_to(ROOT), link))

    # Offline helpers must be present for the two imported cross-cutting skills.
    # Their existence is not evidence that the executable automatically calls them.
    for rel in (
        ".github/scripts/intent_router.py",
        ".github/scripts/development_journal.py",
        ".github/tests/test_intent_orchestrator.py",
        ".github/tests/test_development_journal.py",
        ".github/examples/intent-orchestrator/example-plan.json",
        ".github/examples/intent-orchestrator/README.md",
        ".github/examples/development-journal/README.md",
        ".github/skills/zearth-agent-workflow/SKILL.md",
        "docs/ZEARTH_AGENT_WORKFLOW.md",
    ):
        if not (ROOT / rel).is_file():
            errors.append("Missing required agent workflow companion: " + rel)

    for rel in (
        ".github/references/google-earth-pro-engineering.md",
        ".github/references/historical-imagery-timeline.md",
        ".github/references/google-kml-kmz-compatibility.md",
        ".github/references/world-gazetteer-reference-layer.md",
    ):
        doc = ROOT / rel
        if not doc.is_file():
            errors.append("Missing essential Earth/KML/Gazetteer guidance: " + rel)
            continue
        for link in links_in(doc.read_text(encoding="utf-8")):
            target = (doc.parent / link).resolve()
            if not target.is_relative_to(ROOT) or not target.is_file():
                errors.append("Broken Earth reference link: {} -> {}".format(rel, link))

    for doc in (ROOT / "src").rglob("*.xaml"):
        try:
            ET.parse(doc)
        except ET.ParseError as exc:
            errors.append("Invalid XML/XAML {}: {}".format(doc.relative_to(ROOT), exc))

    for expected in (
        "AGENTS.md", "README.md", "README.ar.md",
        "docs/GIS_SKILLS_INTEGRATION.md", "docs/HISTORICAL_IMAGERY_TIMELINE.md",
    ):
        if not (ROOT / expected).is_file():
            errors.append("Missing fresh-checkout guide: " + expected)

    agents = (ROOT / "AGENTS.md").read_text(encoding="utf-8") if (ROOT / "AGENTS.md").is_file() else ""
    if "zearth-google-earth-pro-workflows" not in agents or "GIS_SKILLS_SNAPSHOT.json" not in agents:
        errors.append("AGENTS.md must route to pinned source and Z EARTH Earth adapter")
    if errors:
        print("\n".join("ERROR: " + item for item in errors))
        return 1
    print("PASS: {} pinned upstream GIS skills, {} references, Earth adapter and XAML checked.".format(
        PINNED_SKILL_COUNT, PINNED_REFERENCE_COUNT))
    print("WARNING: documentation integrity is NOT proof of native WPF/WebView2/Wayback runtime behavior.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
