#!/usr/bin/env python3
"""Offline intent route hints and structural plan/approval declaration validation.

NO LLM, remote call, execution, authorization, file writes or OS isolation.
Run with --text for conservative bilingual keyword hints or --plan for a JSON
draft from a human/AI. --gate verifies declared fields, not user identity.
"""
import argparse
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SKILLS = ROOT / "skills"
DOMAINS = (
    ("kml-kmz-engineering", ("kml", "kmz", "placemark", "كي ام ال", "كى ام ال", "ستايل km", "ملفات الكي")),
    ("gis-desktop-application-development", ("desktop", "exe", "wpf", "زيرث", "zearth", "سطح المكتب", "برنامج مستقل")),
    ("terrain-hydrology-lidar", ("terrain", "dem", "elevation", "lidar", "تضاريس", "ارتفاع", "المبالغة", "الترين")),
    ("coordinate-reference-systems", ("crs", "epsg", "projection", "grid", "graticule", "خطوط الطول", "دوائر العرض", "الإحداثي", "مسقط")),
    ("gis-cartography-symbology", ("symbol", "style", "icon", "symbology", "أيقون", "الرموز", "ألوان", "ملصق")),
    ("arcgis-toolbox-engineering", ("arcgis", "arcpy", "arcmap", "pyt", "أرك جي", "بايثون تول")),
    ("qgis-plugin-development", ("qgis", "pyqgis", "كيو جي")),
    ("geospatial-web-mapping", ("cesium", "maplibre", "web map", "globe", "ثلاثية الأبعاد", "كرة أرضية")),
    ("remote-sensing-pipelines", ("sentinel", "landsat", "gee", "satellite", "الاستشعار", "مرئيات")),
    ("geospatial-data-processing", ("gdal", "geopandas", "raster", "vector", "راستر", "فيكتور")),
    ("spatial-databases", ("postgis", "spatialite", "spatial sql", "قواعد البيانات المكانية")),
    ("geospatial-api-integration", ("api", "geocod", "gazetteer", "أسماء الأماكن", "بحث عن الأماكن")),
    ("geospatial-data-discovery", ("download data", "stac", "مصادر البيانات", "تنزيل بيانات")),
    ("geospatial-ai-agents", ("agent", "geoai", "automation", "وكيل", "الذكاء الاصطناعي")),
)
RISKS = {"low", "medium", "high", "critical"}
SIDE_EFFECTS = {"read", "write", "network", "destructive"}
STATUSES = {"draft", "approved"}


def available_skills(root=SKILLS):
    return {p.parent.name for p in Path(root).glob("*/SKILL.md")}


def route_hints(request, known=None):
    """Lexical hints only; do NOT treat as semantic extraction."""
    if known is None:
        known = available_skills()
    lowered = request.casefold()
    hits = [name for name, tokens in DOMAINS
            if name in known and any(token in lowered for token in tokens)]
    if "gis-security-guardian" in known:
        hits.append("gis-security-guardian")
    return {
        "schema_version": "1.0",
        "stage": "draft_hints_only",
        "original_request": request,
        "language_hint": "ar" if re.search(r"[\u0600-\u06ff]", request) else "en",
        "suggested_skills": list(dict.fromkeys(hits)),
        "requires_semantic_review": True,
        "approval": "not_requested",
        "note": "Keyword matches only. A human/LLM must identify tasks, uncertainties, acceptance tests and obtain approval before tool dispatch."
    }


def _text(value):
    return isinstance(value, str) and bool(value.strip())


def validate_plan(plan, known=None, require_approved=False):
    """Return structural errors, never execute the plan."""
    if known is None:
        known = available_skills()
    errors = []
    if not isinstance(plan, dict):
        return ["Plan must be a JSON object"]
    if plan.get("schema_version") != "1.0":
        errors.append("schema_version must be 1.0")
    for field in ("request_id", "revision", "original_request"):
        if not _text(plan.get(field)):
            errors.append(field + " must be a nonempty string")
    if plan.get("language") not in ("ar", "en"):
        errors.append("language must be ar or en")
    status = plan.get("status")
    if status not in STATUSES:
        errors.append("status must be draft or approved")
    for field in ("assumptions", "open_questions"):
        val = plan.get(field)
        if not isinstance(val, list) or any(not _text(x) for x in val):
            errors.append(field + " must be a list of strings")
    approval = plan.get("approval")
    if not isinstance(approval, dict):
        approval = {}
        errors.append("approval must be an object")
    for field in ("approved_by_user", "high_risk_approved"):
        if type(approval.get(field)) is not bool:
            errors.append("approval." + field + " must be boolean")
    if not isinstance(approval.get("approved_revision"), (str, type(None))):
        errors.append("approval.approved_revision must be string or null")
    tasks = plan.get("tasks")
    if not isinstance(tasks, list) or not tasks:
        return errors + ["tasks must be a nonempty array"]
    ids = set()
    depends = {}
    high_risk = False
    for n, task in enumerate(tasks):
        pref = "tasks[" + str(n) + "]"
        if not isinstance(task, dict):
            errors.append(pref + " must be an object")
            continue
        ident = task.get("id")
        if not _text(ident) or not re.fullmatch(r"[A-Za-z][A-Za-z0-9_-]{0,39}", ident):
            errors.append(pref + ".id is invalid")
            continue
        if ident in ids:
            errors.append("duplicate task ID: " + ident)
        ids.add(ident)
        for field in ("title", "description", "kind"):
            if not _text(task.get(field)):
                errors.append(pref + "." + field + " must be a nonempty string")
        if task.get("priority") not in ("must", "should", "could"):
            errors.append(pref + ".priority invalid")
        risk = task.get("risk")
        if risk not in RISKS:
            errors.append(pref + ".risk invalid")
        if risk in ("high", "critical"):
            high_risk = True
        selected = task.get("skills")
        if not isinstance(selected, list) or not selected or any(not _text(x) or x not in known for x in selected):
            errors.append(pref + ".skills contain unknown or missing skill")
        if isinstance(selected, list) and "gis-security-guardian" not in selected:
            errors.append(pref + ".skills must include gis-security-guardian")
        deps = task.get("depends_on")
        if not isinstance(deps, list) or any(not _text(x) for x in deps):
            errors.append(pref + ".depends_on must be string array")
            deps = []
        if ident in deps:
            errors.append(pref + " cannot depend on itself")
        if len(deps) != len(set(deps)):
            errors.append(pref + " contains duplicate dependencies")
        depends[ident] = deps
        criteria = task.get("acceptance_criteria")
        if not isinstance(criteria, list) or not criteria or any(not _text(x) for x in criteria):
            errors.append(pref + ".acceptance_criteria must be nonempty string array")
        tools = task.get("tools", [])
        if not isinstance(tools, list):
            errors.append(pref + ".tools must be array")
            continue
        for item in tools:
            if not isinstance(item, dict) or not _text(item.get("tool_id")) or item.get("side_effect") not in SIDE_EFFECTS:
                errors.append(pref + ".tools entries need tool_id and valid side_effect")
            elif item["side_effect"] == "destructive":
                high_risk = True
    for ident, deps in depends.items():
        for dep in deps:
            if dep not in ids:
                errors.append(ident + " depends on missing task " + dep)
    visiting, visited = set(), set()
    def walk(node):
        if node in visiting:
            errors.append("dependency cycle at " + node)
            return
        if node in visited:
            return
        visiting.add(node)
        for dep in depends.get(node, []):
            if dep in depends:
                walk(dep)
        visiting.remove(node)
        visited.add(node)
    for ident in depends:
        walk(ident)
    if require_approved or status == "approved":
        if status != "approved" or approval.get("approved_by_user") is not True:
            errors.append("execution gate: plan is not user-approved")
        if not _text(plan.get("revision")) or approval.get("approved_revision") != plan.get("revision"):
            errors.append("execution gate: approval revision mismatch")
        if high_risk and approval.get("high_risk_approved") is not True:
            errors.append("execution gate: high-risk permission not declared")
    return errors


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__)
    action = p.add_mutually_exclusive_group(required=True)
    action.add_argument("--text", help="Unstructured input; get lexical skill hints, NOT AI understanding")
    action.add_argument("--plan", type=Path, help="Structured JSON plan drafted by an agent or human")
    p.add_argument("--format", choices=("text", "json"), default="text")
    p.add_argument("--gate", action="store_true", help="Check declared revision-scoped approval; never execute")
    args = p.parse_args(argv)
    if args.text is not None:
        if args.gate:
            p.error("--gate requires --plan")
        out = route_hints(args.text)
        if args.format == "json":
            print(json.dumps(out, ensure_ascii=False, indent=2))
        else:
            print("Possible skills (keyword hints only):", ", ".join(out["suggested_skills"]))
            print(out["note"])
        return 0
    try:
        plan = json.loads(args.plan.read_text(encoding="utf-8"))
    except (OSError, ValueError) as exc:
        print("Invalid plan file: " + str(exc), file=sys.stderr)
        return 2
    errors = validate_plan(plan, require_approved=args.gate)
    if args.format == "json":
        print(json.dumps({"valid": not errors, "approved_gate_declared": bool(args.gate and not errors),
                          "errors": errors, "note": "No execution or authenticated approval is performed."}, indent=2))
    else:
        for issue in errors:
            print("ERROR: " + issue)
        if not errors:
            print("Plan structure valid" + ("; approval declarations present (NOT authenticated)" if args.gate else "; review still required"))
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
