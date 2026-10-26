#!/usr/bin/env python3
"""Verify source-license metadata and independent third-party notices (offline).

This is a consistency check only, NOT an audit of actual redistributed binaries,
asset ownership, GPL linkage requirements or proprietary vendor licenses.
"""
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
LICENSE = ROOT / "LICENSE"
PROJECTS = (
    "src/ZEarth.App/ZEarth.App.csproj",
    "src/ZEarth.Domain/ZEarth.Domain.csproj",
    "src/ZEarth.Globe/ZEarth.Globe.csproj",
)
DOCS = {
    "README.md": ("GPL-3.0-only", "(LICENSE)"),
    "README.ar.md": ("GPL-3.0-only", "(LICENSE)"),
    "LEGAL_AND_PRIVACY_NOTICES.md": ("GPL-3.0-only", "THIRD_PARTY_LICENSES.md"),
    "THIRD_PARTY_LICENSES.md": ("GPL-3.0-only", "CesiumJS", "WebView2", "GDAL"),
    "docs/LICENSE_STATUS.md": ("GPL-3.0-only", "LICENSE", "outstanding"),
}


def validate() -> list[str]:
    errors: list[str] = []
    if not LICENSE.is_file():
        errors.append("Missing root GPL license")
    else:
        text = LICENSE.read_text(encoding="utf-8")
        for token in (
            "GNU GENERAL PUBLIC LICENSE",
            "Version 3, 29 June 2007",
            "TERMS AND CONDITIONS",
            "END OF TERMS AND CONDITIONS",
            "How to Apply These Terms to Your New Programs",
        ):
            if token not in text:
                errors.append(f"GPL license missing section: {token}")

    preview_packaging = ROOT / "scripts/create-portable-preview.ps1"
    try:
        packaging = preview_packaging.read_text(encoding="utf-8")
        for required in ("'LICENSE'", "'THIRD_PARTY_LICENSES.md'", "'LEGAL_AND_PRIVACY_NOTICES.md'"):
            if required not in packaging:
                errors.append(f"Portable build does not explicitly package {required}")
    except OSError as exc:
        errors.append(f"Portable package script unreadable: {exc}")

    for project in PROJECTS:
        try:
            root = ET.parse(ROOT / project).getroot()
            metadata = [
                (x.text or "").strip()
                for x in root.iter("PackageLicenseExpression")
            ]
            if metadata != ["GPL-3.0-only"]:
                errors.append(f"{project}: expected GPL-3.0-only metadata, got {metadata}")
        except (ET.ParseError, OSError) as exc:
            errors.append(f"{project}: cannot read valid XML: {exc}")

    for path, requirements in DOCS.items():
        try:
            content = (ROOT / path).read_text(encoding="utf-8")
            for token in requirements:
                if token not in content:
                    errors.append(f"{path}: missing {token}")
        except OSError as exc:
            errors.append(f"{path}: missing/unreadable: {exc}")

    try:
        skill_notice = (ROOT / ".github/LICENSE").read_text(encoding="utf-8")
        if not skill_notice.startswith("MIT License"):
            errors.append(".github/LICENSE: independent MIT text has changed")
    except OSError as exc:
        errors.append(f".github/LICENSE: missing/unreadable: {exc}")
    return errors


if __name__ == "__main__":
    problems = validate()
    if problems:
        print("License boundary check FAILED:")
        for problem in problems:
            print(f"- {problem}")
        sys.exit(1)
    print("License boundary consistency check OK (source metadata only; no release certification).")
