"""Exercise conditional routing and honest evidence handoffs; no external renderer."""
import copy
import importlib.util
import json
import subprocess
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("archify_intent", ROOT / "scripts/intent_router.py")
router = importlib.util.module_from_spec(spec)
spec.loader.exec_module(router)
SKILL = "gis-architecture-visualization"


class ArchifyIntegrationTests(unittest.TestCase):
    def setUp(self):
        self.plan = json.loads((ROOT / "examples/architecture-visualization/plan.example.json").read_text("utf-8"))

    def test_explicit_english_and_arabic_requests_are_discoverable(self):
        for request in ("Draw a diagram of this project's architecture", "ارسملي مخطط يوضح مين مسؤول عن مين"):
            self.assertIn(SKILL, router.route_hints(request)["suggested_skills"])
        self.assertNotIn(SKILL, router.route_hints("Change one button label and its color")["suggested_skills"])
        self.assertNotIn(SKILL, router.route_hints("غيّر اسم الزر ولون الأيقونة")["suggested_skills"])

    def test_draft_has_a_handoff_without_execution_or_runtime_claims(self):
        result = router.compile_workflow(self.plan)
        self.assertTrue(result["valid"], result["errors"])
        handoff = result["steps"][0]["visualization"]
        self.assertEqual(handoff["renderer"], "archify")
        self.assertEqual(handoff["evidence_basis"], "source-reviewed")
        self.assertEqual(handoff["status"], "NOT_STARTED")
        self.assertFalse(handoff["runtime_verified"])
        self.assertFalse(handoff["gis_execution_proven"])
        self.assertFalse(result["can_execute"])
        self.assertFalse(result["host_permissions_verified"])
        self.assertFalse(router.compile_workflow(self.plan, require_approved=True)["valid"])

    def test_supporting_visualization_retains_diagnostic_domain_owner(self):
        task = self.plan["tasks"][0]
        task["skills"].append("gis-data-flow-diagnostics")
        task["owner_skill"] = "gis-data-flow-diagnostics"
        task["visualization"].update(purpose="cross-component-diagnosis", diagram_type="dataflow")
        result = router.compile_workflow(self.plan)
        self.assertTrue(result["valid"], result["errors"])
        self.assertEqual(result["steps"][0]["owner_skill"], "gis-data-flow-diagnostics")
        self.assertEqual(result["steps"][0]["visualization"]["artifact_owner"], SKILL)

    def test_visualization_requires_selected_specialist_and_complete_metadata(self):
        cases = []
        missing = copy.deepcopy(self.plan)
        del missing["tasks"][0]["visualization"]
        cases.append(missing)
        unselected = copy.deepcopy(self.plan)
        unselected["tasks"][0]["skills"] = ["geospatial-ai-agents", "gis-security-guardian"]
        cases.append(unselected)
        for key in router.VISUALIZATION_FIELDS:
            for bad_value in (None, [], {}, "invented"):
                invalid = copy.deepcopy(self.plan)
                invalid["tasks"][0]["visualization"][key] = bad_value
                cases.append(invalid)
        injected = copy.deepcopy(self.plan)
        injected["tasks"][0]["visualization"]["runtime_verified"] = True
        cases.append(injected)
        for plan in cases:
            with self.subTest(plan=plan["tasks"][0].get("visualization")):
                self.assertFalse(router.compile_workflow(plan)["valid"])

    def test_existing_nonvisual_tasks_still_compile_without_new_dependency(self):
        baseline = json.loads((ROOT / "examples/intent-orchestrator/example-plan.json").read_text("utf-8"))
        result = router.compile_workflow(baseline)
        self.assertTrue(result["valid"], result["errors"])
        self.assertTrue(all("visualization" not in step for step in result["steps"]))

    def test_cli_emits_handoff_and_preserves_unapproved_gate(self):
        command = [sys.executable, str(ROOT / "scripts/intent_router.py"), "--plan",
                   str(ROOT / "examples/architecture-visualization/plan.example.json"),
                   "--workflow", "--format", "json"]
        preview = subprocess.run(command, cwd=ROOT, capture_output=True, text=True)
        self.assertEqual(preview.returncode, 0, preview.stderr)
        self.assertEqual(json.loads(preview.stdout)["steps"][0]["visualization"]["renderer"], "archify")
        blocked = subprocess.run(command + ["--gate"], cwd=ROOT, capture_output=True, text=True)
        self.assertEqual(blocked.returncode, 1)
        self.assertEqual(json.loads(blocked.stdout)["status"], "BLOCKED")


if __name__ == "__main__":
    unittest.main()

