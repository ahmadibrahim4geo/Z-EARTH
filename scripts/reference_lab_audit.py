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
import re
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
        ("src/ZEarth.App/Views/MainWindow.xaml", 'Command="{Binding'),
        ("src/ZEarth.App/ViewModels/MainViewModel.cs", "public ICommand"),
    ],
    "Z02": [
        ("src/ZEarth.Globe/Assets/cesium-engine.js", "setSceneMode"),
        ("src/ZEarth.Globe/GlobeBridge.cs", "SendRequestAsync"),
        ("src/ZEarth.Globe/Assets/index.html", "cesium-engine.js"),
    ],
    "Z03": [
        ("src/ZEarth.Domain/Kml/KmlParser.cs", "DtdProcessing.Prohibit"),
        ("src/ZEarth.Domain/Kml/KmlParser.cs", 'e.Name.LocalName.Equals("Document"'),
        ("src/ZEarth.Domain/Kml/KmzService.cs", "MaxAllowedDecompressedBytes"),
        ("tests/ZEarth.Tests/KmlLegacyGoogleEarth21Tests.cs", "Earth21DocumentRecognized"),
    ],
    "Z04": [
        ("src/ZEarth.Globe/Assets/cesium-engine.js", "GraticuleManager"),
        ("src/ZEarth.Globe/Assets/cesium-engine.js", "EnvironmentManager"),
        ("src/ZEarth.App/ViewModels/MainViewModel.cs", "SearchByNameCommand"),
    ],
}

def analyze_source(role: str) -> tuple[dict, list[str]]:
    """Semantic source *contract* coverage only, never a native execution claim."""
    metrics: dict = {}
    issues: list[str] = []
    if role == "Z01":
        xaml = (ROOT / "src/ZEarth.App/Views/MainWindow.xaml").read_text(encoding="utf-8")
        viewmodel = (ROOT / "src/ZEarth.App/ViewModels/MainViewModel.cs").read_text(encoding="utf-8")
        names = set(re.findall(r'Command="\{Binding\s+([A-Za-z]\w*)', xaml))
        declarations = set(re.findall(r'\bpublic\s+ICommand\s+([A-Za-z]\w+)\b', viewmodel))
        commands = {x for x in names if x.endswith("Command")}
        unresolved = sorted(commands - declarations)
        metrics = {
            "wpf_command_bindings": len(re.findall(r'Command="\{Binding\s+([A-Za-z]\w*)', xaml)),
            "unique_wpf_commands": len(commands),
            "main_viewmodel_icommand_declarations": len(declarations),
            "unresolved_mainwindow_commands": unresolved,
        }
        if unresolved:
            issues.append("Unresolved MainWindow ICommand bindings: " + ", ".join(unresolved))
    elif role == "Z02":
        bridge = (ROOT / "src/ZEarth.Globe/GlobeBridge.cs").read_text(encoding="utf-8")
        javascript = (ROOT / "src/ZEarth.Globe/Assets/cesium-engine.js").read_text(encoding="utf-8")
        actions = set(re.findall(r'SendRequestAsync\(\s*"([^"]+)"', bridge))
        javascript_cases = set(re.findall(r'case\s+[\'"]([^\'"]+)[\'"]\s*:', javascript))
        unmatched = sorted(actions - javascript_cases)
        metrics = {
            "unique_bridge_actions": len(actions),
            "js_switch_cases": len(javascript_cases),
            "unmatched_bridge_actions": unmatched,
        }
        if unmatched:
            issues.append("Bridge commands not recognized by JS switch: " + ", ".join(unmatched))
    elif role == "Z03":
        parser = (ROOT / "src/ZEarth.Domain/Kml/KmlParser.cs").read_text(encoding="utf-8")
        tests = (ROOT / "tests/ZEarth.Tests/KmlLegacyGoogleEarth21Tests.cs").read_text(encoding="utf-8")
        metrics = {
            "legacy_kml_document_element_supported": 'e.Name.LocalName.Equals("Document"' in parser,
            "xxe_dtd_prohibited": "DtdProcessing.Prohibit" in parser,
            "has_synthetic_legacy_kml_fixture": "http://earth.google.com/kml/2.1" in tests,
        }
        if not all(metrics.values()):
            issues.append("KML parser/source regression contract missing")
    elif role == "Z04":
        js = (ROOT / "src/ZEarth.Globe/Assets/cesium-engine.js").read_text(encoding="utf-8")
        vm = (ROOT / "src/ZEarth.App/ViewModels/MainViewModel.cs").read_text(encoding="utf-8")
        checks = {
            "grid_master_visibility_gate": "this.dataSource.show = this.config.visible" in js,
            "independent_longitude_interval": "longitudeIntervalDegrees" in js,
            "independent_latitude_interval": "latitudeIntervalDegrees" in js,
            "sunlight_bridge_readback": "SunlightChanged" in vm,
            "atmosphere_bridge_readback": "AtmosphereChanged" in vm,
            "offline_search_command": "SearchByNameCommand" in vm,
        }
        metrics = checks
        if not all(checks.values()):
            issues.append("Grid/environment/search source gate missing")
    return metrics, issues


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
    metrics, issues = analyze_source(role)
    passed = all(item["found"] for item in probes) and not issues
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
        "source_metrics": metrics,
        "issues": issues,
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
