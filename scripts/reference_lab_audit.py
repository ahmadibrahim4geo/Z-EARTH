#!/usr/bin/env python3
"""Evidence-only reference lab role runner.

Twelve deterministic auditors run read-only source checks. This tool cannot
spawn LLM agents, execute a proprietary reference, or certify native GUI parity.
Output labels intentionally distinguish metadata and source contracts.
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "docs/google-earth-reference/AGENT_MANIFEST.json"

# Each probe must find a plain text contract, not an executable or DLL.
PROBES = {
    "R01": [("docs/google-earth-reference/reports/R01.md", "googleearth.exe")],
    "R02": [("docs/google-earth-reference/reports/R02.md", "toolbar.rcc")],
    "R03": [("docs/google-earth-reference/reports/R03.md", "navcontrols.rcc")],
    "R04": [("docs/google-earth-reference/reports/R04.md", "atmosphere.glslesf")],
    "R05": [("docs/google-earth-reference/reports/R05.md", "userpalette.kml")],
    "R06": [("docs/google-earth-reference/reports/R06.md", "Independent axis spacing")],
    "R07": [("docs/google-earth-reference/reports/R07.md", "search.rcc")],
    "R08": [("docs/google-earth-reference/reports/R08.md", "flightsim")],
    "Z01": [
        ("src/ZEarth.App/Views/MainWindow.xaml", "ToggleCloudsCommand"),
        ("src/ZEarth.App/ViewModels/MainViewModel.cs", "ExecuteToggleCloudsAsync"),
    ],
    "Z02": [
        ("src/ZEarth.Globe/Assets/cesium-engine.js", "environment.setClouds"),
        ("src/ZEarth.Globe/GlobeBridge.cs", "SetCloudsAsync"),
        ("src/ZEarth.Globe/Assets/index.html", "cloud-layer.js"),
    ],
    "Z03": [
        ("src/ZEarth.Domain/Kml/KmlParser.cs", "DtdProcessing.Prohibit"),
        ("src/ZEarth.Domain/Kml/KmzService.cs", "MaxAllowedDecompressedBytes"),
    ],
    "Z04": [
        ("src/ZEarth.Globe/Assets/cesium-engine.js", "GraticuleManager"),
        ("src/ZEarth.Globe/Assets/cesium-engine.js", "EnvironmentManager"),
        ("src/ZEarth.App/Services/SettingsService.cs", "ShowClouds"),
    ],
}

def evaluate(role: str) -> dict:
    probes = []
    for relative, needle in PROBES[role]:
        file_path = (ROOT / relative).resolve()
        if not file_path.is_relative_to(ROOT.resolve()):
            raise ValueError("Path escapes repository")
        try:
            found = needle in file_path.read_text(encoding="utf-8")
        except OSError:
            found = False
        probes.append({"source": relative, "contains": needle, "found": found})
    passed = all(item["found"] for item in probes)
    reference = role.startswith("R")
    return {
        "schema": "zearth.reference-lab.evidence.v1",
        "role": role,
        "completed_utc": dt.datetime.now(dt.timezone.utc).isoformat(),
        "source_contract_check": "PASS" if passed else "FAIL",
        "evidence_level": "REFERENCE_METADATA_ONLY" if reference else "ZE_SOURCE_ONLY",
        "native_windows_reference_executed": False,
        "native_windows_zearth_executed": False,
        "independent_llm_agent_launched": False,
        "runtime_parity": "NOT_VERIFIED",
        "probes": probes,
    }

def main() -> int:
    p = argparse.ArgumentParser(description=__doc__)
    target = p.add_mutually_exclusive_group(required=True)
    target.add_argument("--role", choices=list(PROBES))
    target.add_argument("--all", action="store_true")
    p.add_argument("--out-dir", default="reference-evidence")
    args = p.parse_args()
    registry = json.loads(MANIFEST.read_text(encoding="utf-8"))
    registered = {x["id"] for x in registry["agents"]}
    if registered != set(PROBES):
        raise SystemExit("Manifest/workstream registry disagreement")
    out = Path(args.out_dir)
    out.mkdir(parents=True, exist_ok=True)
    roles = list(PROBES) if args.all else [args.role]
    failure = False
    for role in roles:
        report = evaluate(role)
        (out / f"{role}.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
        failure |= report["source_contract_check"] != "PASS"
        print(f"{role}: {report['source_contract_check']} ({report['evidence_level']}; runtime NOT_VERIFIED)")
    return 1 if failure else 0

if __name__ == "__main__":
    raise SystemExit(main())
