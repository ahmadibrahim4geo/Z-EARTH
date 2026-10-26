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

    def test_specialist_routing_hints_across_repository(self):
        cases = (
            ("Integrate QGIS Core in a standalone app", "qgis-engine-integration"),
            ("Build a QGIS plugin for Processing", "qgis-plugin-development"),
            ("Use Global Mapper script automation", "global-mapper-development"),
            ("Build a GIS data catalog", "geospatial-data-catalogs"),
            ("تعريب الواجهة وجعلها ثنائي اللغة", "gis-internationalization-arabic"),
            ("Solar position and device time zone", "gis-device-context"),
            ("فهرس البيانات الجغرافية", "geospatial-data-catalogs"),
        )
        for prompt, expected_skill in cases:
            with self.subTest(prompt=prompt):
                hints = module.route_hints(prompt)
                self.assertIn(expected_skill, hints["suggested_skills"])
                self.assertIn("gis-security-guardian", hints["suggested_skills"])
                self.assertTrue(hints["requires_semantic_review"])
                self.assertEqual("draft_hints_only", hints["stage"])

    def test_qgis_engine_is_not_silently_classified_as_plugin(self):
        hints = module.route_hints("Use QGIS engine libraries")
        self.assertIn("qgis-engine-integration", hints["suggested_skills"])
        self.assertNotIn("qgis-plugin-development", hints["suggested_skills"])
        generic = module.route_hints("I need QGIS help")
        self.assertTrue(generic["requires_semantic_review"])
        self.assertNotIn("qgis-plugin-development", generic["suggested_skills"])

    def test_prompt_requires_semantic_goal_review(self):
        hints = module.route_hints("عايز برنامج مستقل لعرض خرائط KML وDEM")
        self.assertIsNone(hints["goal"])
        self.assertIsNone(hints["target"])
        self.assertEqual([], hints["desired_outcomes"])
        self.assertTrue(hints["requires_semantic_review"])
        self.assertIn("no execution", hints["next_step"])

    def test_workflow_compiles_goal_and_dependencies_without_execution(self):
        blueprint = module.compile_workflow(self.plan)
        self.assertTrue(blueprint["valid"], blueprint["errors"])
        self.assertEqual("workflow_preview_only", blueprint["kind"])
        self.assertEqual("AWAITING_REVIEW", blueprint["status"])
        self.assertEqual(self.plan["goal"], blueprint["goal"])
        self.assertEqual(self.plan["desired_outcomes"], blueprint["desired_outcomes"])
        self.assertEqual(self.plan["success_criteria"], blueprint["success_criteria"])
        self.assertFalse(blueprint["can_execute"])
        self.assertFalse(blueprint["host_permissions_verified"])
        self.assertFalse(blueprint["authenticated_user_approval"])
        steps = blueprint["steps"]
        self.assertEqual(["T1", "T2", "T3"], [x["task_id"] for x in steps])
        self.assertEqual("gis-desktop-application-development", steps[0]["owner_skill"])
        self.assertIn("security guardian", steps[1]["preflight"])
        self.assertEqual({("T1", "T3"), ("T2", "T3")},
                         {(e["from_task"], e["to_task"]) for e in blueprint["handoffs"]})

    def test_workflow_blocks_missing_goal_and_outcome(self):
        p = deepcopy(self.plan)
        for key in ("goal", "desired_outcomes", "success_criteria", "target"):
            p.pop(key)
        blueprint = module.compile_workflow(p)
        self.assertFalse(blueprint["valid"])
        self.assertEqual("BLOCKED", blueprint["status"])
        self.assertEqual([], blueprint["steps"])
        self.assertTrue(any("goal" in x for x in blueprint["errors"]))

    def test_workflow_blocks_invalid_skill_ownership(self):
        p = deepcopy(self.plan)
        p["tasks"][0]["owner_skill"] = "not-installed-specialist"
        blueprint = module.compile_workflow(p)
        self.assertEqual("BLOCKED", blueprint["status"])
        self.assertTrue(any("owner_skill" in x for x in blueprint["errors"]))
        p["tasks"][0]["owner_skill"] = "gis-security-guardian"
        self.assertFalse(module.compile_workflow(p)["valid"])

    def test_workflow_invalid_dependency_cannot_produce_steps(self):
        p = deepcopy(self.plan)
        p["tasks"][0]["depends_on"] = ["T3"]
        self.assertFalse(module.compile_workflow(p)["valid"])
        self.assertEqual([], module.compile_workflow(p)["steps"])

    def test_declared_approval_still_never_executes(self):
        unapproved = module.compile_workflow(self.plan, require_approved=True)
        self.assertEqual("BLOCKED", unapproved["status"])
        p = deepcopy(self.plan)
        p["status"] = "approved"
        p["approval"]["approved_by_user"] = True
        p["approval"]["approved_revision"] = p["revision"]
        blueprint = module.compile_workflow(p, require_approved=True)
        self.assertTrue(blueprint["valid"], blueprint["errors"])
        self.assertEqual("AWAITING_HOST_AUTHORIZATION", blueprint["status"])
        self.assertTrue(blueprint["approval_declarations_checked"])
        self.assertFalse(blueprint["authenticated_user_approval"])
        self.assertFalse(blueprint["can_execute"])
        p["revision"] = "r2"
        self.assertEqual("BLOCKED", module.compile_workflow(p, require_approved=True)["status"])

    def test_workflow_requires_extra_permission_for_destructive_operations(self):
        p = deepcopy(self.plan)
        p["tasks"][0]["tools"] = [{"tool_id": "host.remove", "side_effect": "destructive"}]
        p["status"] = "approved"
        p["approval"]["approved_by_user"] = True
        p["approval"]["approved_revision"] = p["revision"]
        self.assertEqual("BLOCKED", module.compile_workflow(p, require_approved=True)["status"])
        p["approval"]["high_risk_approved"] = True
        self.assertTrue(module.compile_workflow(p, require_approved=True)["valid"])

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
