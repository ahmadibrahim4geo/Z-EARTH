"""Offline intent orchestration validation and approval gates."""
import importlib.util
import json
import unittest
from copy import deepcopy
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("intent_router", ROOT / "scripts" / "intent_router.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
FIXTURE = ROOT / "examples" / "intent-orchestrator" / "example-plan.json"


class IntentOrchestrationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.plan = json.loads(FIXTURE.read_text(encoding="utf-8"))

    def test_bilingual_text_only_gives_hints(self):
        ar = module.route_hints("عايز KML و Terrain في ZEarth")
        self.assertIn("kml-kmz-engineering", ar["suggested_skills"])
        self.assertIn("gis-desktop-application-development", ar["suggested_skills"])
        self.assertIn("gis-security-guardian", ar["suggested_skills"])
        self.assertTrue(ar["requires_semantic_review"])
        en = module.route_hints("ArcGIS Pro toolbar and grid")
        self.assertIn("arcgis-toolbox-engineering", en["suggested_skills"])

    def test_unapproved_example_is_valid_draft_but_fails_gate(self):
        self.assertEqual([], module.validate_plan(self.plan))
        self.assertTrue(any("user-approved" in error for error in
                            module.validate_plan(self.plan, require_approved=True)))

    def test_revision_scope_approval(self):
        p = deepcopy(self.plan)
        p["status"] = "approved"
        p["approval"]["approved_by_user"] = True
        p["approval"]["approved_revision"] = "r1"
        self.assertEqual([], module.validate_plan(p, require_approved=True))
        p["revision"] = "r2"
        self.assertTrue(any("revision mismatch" in x for x in module.validate_plan(p, require_approved=True)))

    def test_cycle_and_missing_dependency_fail(self):
        p = deepcopy(self.plan)
        p["tasks"][0]["depends_on"] = ["T3"]
        self.assertTrue(any("cycle" in x for x in module.validate_plan(p)))
        p["tasks"][0]["depends_on"] = ["DOES-NOT-EXIST"]
        self.assertTrue(any("missing task" in x for x in module.validate_plan(p)))

    def test_security_and_unknown_skill_fail(self):
        p = deepcopy(self.plan)
        p["tasks"][0]["skills"] = ["fictional-executor"]
        errors = module.validate_plan(p)
        self.assertTrue(any("unknown" in x for x in errors))
        self.assertTrue(any("guardian" in x for x in errors))

    def test_high_risk_requires_separate_permission(self):
        p = deepcopy(self.plan)
        p["status"] = "approved"
        p["approval"]["approved_by_user"] = True
        p["approval"]["approved_revision"] = "r1"
        p["tasks"][0]["risk"] = "high"
        self.assertTrue(any("high-risk" in x for x in module.validate_plan(p, require_approved=True)))
        p["approval"]["high_risk_approved"] = True
        self.assertEqual([], module.validate_plan(p, require_approved=True))

    def test_destructive_tool_also_requires_high_risk_permission(self):
        p = deepcopy(self.plan)
        p["tasks"][0]["tools"] = [{"tool_id": "host.delete-source", "side_effect": "destructive"}]
        p["status"] = "approved"
        p["approval"]["approved_by_user"] = True
        p["approval"]["approved_revision"] = "r1"
        self.assertTrue(any("high-risk" in x for x in module.validate_plan(p, require_approved=True)))


if __name__ == "__main__":
    unittest.main()
