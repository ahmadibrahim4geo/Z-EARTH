"""Real offline tests for four new GIS UI quality specialists.

The tested scanner and trace validator operate on tiny synthetic data only.
No Windows session, QGIS/ArcGIS engine or browser automation is simulated as PASS.
"""
import importlib.util
import json
import subprocess
import sys
import tempfile
import unittest
from copy import deepcopy
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SKILLS = (
    "gis-ui-command-audit",
    "gis-desktop-e2e-testing",
    "gis-data-flow-diagnostics",
    "gis-ui-ux-design-testing",
)
SKILL_REFS = (
    "gis-ui-command-audit",
    "gis-desktop-e2e-testing",
    "gis-data-flow-diagnostics",
    "gis-ui-ux-design-testing",
)
EXAMPLE_UI = ROOT / "examples" / "ui-command-audit" / "fixture"
EXAMPLE_TRACE = ROOT / "examples" / "data-flow-diagnostics" / "trace.example.json"


def load_module(name):
    path = ROOT / "scripts" / (name + ".py")
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


ui = load_module("gis_ui_command_audit")
flow = load_module("gis_data_flow_diagnostics")
intent = load_module("intent_router")


class UISpecialistIntegrationTests(unittest.TestCase):
    def test_four_skill_manifests_and_owner_rules(self):
        router = (ROOT / "skills/gis-development/SKILL.md").read_text(encoding="utf-8")
        contract = (ROOT / "references/gis-ui-quality-coordination.md").read_text(encoding="utf-8")
        sources = (ROOT / "references/gis-ui-quality-upstream-sources.md").read_text(encoding="utf-8")
        for name in SKILLS:
            with self.subTest(skill=name):
                skill = ROOT / "skills" / name / "SKILL.md"
                self.assertTrue(skill.is_file())
                text = skill.read_text(encoding="utf-8")
                self.assertIn("name: " + name, text)
                self.assertIn("gis-ui-quality-coordination.md", text)
                self.assertIn(name + "/SKILL.md", router)
                self.assertIn(name + "/SKILL.md", contract)
                self.assertIn("Security Guardian", text)
        for upstream in ("FlaUI/FlaUI", "microsoft/playwright", "dotnet/roslyn",
                         "opentelemetry-dotnet", "penpot/penpot", "axe-windows"):
            self.assertIn(upstream, sources)
        self.assertIn("one owner", contract.lower())

    def test_bilingual_router_is_draft_only(self):
        prompts = [
            ("Audit ribbon commands and find dead buttons", SKILLS[0]),
            ("فحص الأوامر والأيقونات غير المربوطة", SKILLS[0]),
            ("Use UI Automation and WebView2 testing", SKILLS[1]),
            ("اختبار تشغيل الواجهة في تطبيق ويندوز", SKILLS[1]),
            ("Trace GIS data flow from import to export", SKILLS[2]),
            ("عايز تشخيص تدفق البيانات", SKILLS[2]),
            ("Need GIS UI/UX design and accessibility audit", SKILLS[3]),
            ("عايز تصميم الواجهة وتحسين تجربة المستخدم", SKILLS[3]),
        ]
        for prompt, expected in prompts:
            with self.subTest(prompt=prompt):
                hints = intent.route_hints(prompt)
                self.assertIn(expected, hints["suggested_skills"])
                self.assertIn("gis-security-guardian", hints["suggested_skills"])
                self.assertTrue(hints["requires_semantic_review"])
                self.assertIsNone(hints["goal"])
                self.assertEqual(hints["stage"], "draft_hints_only")


class StaticUIAuditTests(unittest.TestCase):
    def test_known_xaml_and_html_inventory(self):
        report = ui.audit_tree(EXAMPLE_UI)
        self.assertEqual(2, report["files_scanned"])
        self.assertEqual(8, report["controls_found"])
        self.assertEqual({"DECLARED_BINDING": 6, "NO_BINDING": 1, "UNINSPECTED": 1},
                         report["status_counts"])
        self.assertEqual([], report["duplicate_declared_ids"])
        self.assertEqual([], report["missing_icon_assets"])
        save = next(item for item in report["entries"] if item["ui_element_id"] == "save-project")
        self.assertEqual("icon.svg", save["icon_ref"])

        self.assertEqual([], report["errors"])
        self.assertTrue(all(i["runtime_status"] == "NOT RUN" for i in report["entries"]))
        statuses = {i["ui_element_id"]: i["static_status"] for i in report["entries"]}
        self.assertEqual("NO_BINDING", statuses["PlaceholderMenu"])
        self.assertEqual("UNINSPECTED", statuses["dynamic-layer-toggle"])
        self.assertEqual("DECLARED_BINDING", statuses["ImportKmlMenu"])
        self.assertFalse(report["engine_execution"])
        self.assertFalse(report["native_ui_tested"])

    def test_duplicate_ids_in_one_view_are_reported(self):
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp) / "page.html"
            p.write_text("<button id='same'>One</button><button id='same'>Two</button>",
                         encoding="utf-8")
            result = ui.audit_tree(tmp)
            self.assertIn("page.html#same", result["duplicate_declared_ids"])

    def test_id_in_separate_views_is_not_a_duplicate(self):
        with tempfile.TemporaryDirectory() as tmp:
            for name in ("one.html", "two.html"):
                (Path(tmp) / name).write_text("<button id='open'>Open</button>", encoding="utf-8")
            self.assertEqual([], ui.audit_tree(tmp)["duplicate_declared_ids"])

    def test_missing_relative_icon_is_detected_without_fetching(self):
        with tempfile.TemporaryDirectory() as tmp:
            (Path(tmp) / "panel.html").write_text(
                "<button id='save' data-command='save' data-icon='missing.svg'>Save</button>",
                encoding="utf-8")
            result = ui.audit_tree(tmp)
            self.assertEqual(1, len(result["missing_icon_assets"]))
            self.assertEqual("icon asset missing", result["missing_icon_assets"][0]["reason"])
        # A remotely referenced icon is not downloaded or treated as a broken local file.
        with tempfile.TemporaryDirectory() as tmp:
            (Path(tmp) / "panel.html").write_text(
                '<button id="save" data-icon="https://example.com/icon.svg">Save</button>',
                encoding="utf-8")
            result = ui.audit_tree(tmp)
            self.assertEqual([], result["missing_icon_assets"])

    def test_broken_or_dtd_xaml_is_reported_and_not_executed(self):
        with tempfile.TemporaryDirectory() as tmp:
            (Path(tmp) / "bad.xaml").write_text("<Window><Button>", encoding="utf-8")
            report = ui.audit_tree(tmp)
            self.assertEqual(1, len(report["errors"]))
            (Path(tmp) / "bad.xaml").write_text("<!DOCTYPE foo><Window/>", encoding="utf-8")
            self.assertIn("DTD", ui.audit_tree(tmp)["errors"][0]["error"])

    def test_cli_json_mode_and_errors(self):
        result = subprocess.run([sys.executable, str(ROOT / "scripts/gis_ui_command_audit.py"),
                                 "--root", str(EXAMPLE_UI), "--format", "json"],
                                capture_output=True, text=True, timeout=20)
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(8, json.loads(result.stdout)["controls_found"])
        bad = subprocess.run([sys.executable, str(ROOT / "scripts/gis_ui_command_audit.py"),
                              "--root", str(ROOT / "does-not-exist")],
                             capture_output=True, text=True, timeout=20)
        self.assertNotEqual(0, bad.returncode)


class GISDataFlowDiagnosticsTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.good = json.loads(EXAMPLE_TRACE.read_text(encoding="utf-8"))

    def test_lossless_fixture_passes_without_real_runtime_claim(self):
        result = flow.validate_trace(self.good)
        self.assertTrue(result["valid"], result["issues"])
        self.assertEqual(2, result["feature_counts"]["READBACK"])
        self.assertFalse(result["native_readback_verified"])
        self.assertFalse(result["render_verified"])
        self.assertEqual([], result["issues"])

    def test_lossless_cannot_omit_readback_from_required_phases(self):
        t = deepcopy(self.good)
        t["required_phases"].remove("READBACK")
        t["events"] = [event for event in t["events"] if event["phase"] != "READBACK"]
        issues = flow.validate_trace(t)["issues"]
        self.assertTrue(any("must require READBACK" in issue for issue in issues), issues)

    def test_export_and_readback_must_refer_to_same_artifact(self):
        t = deepcopy(self.good)
        t["events"][-1]["artifact_id"] = "a-different-export"
        issues = flow.validate_trace(t)["issues"]
        self.assertTrue(any("artifact_id disagree" in issue for issue in issues), issues)

    def test_lossless_count_mismatch_is_detected(self):
        t = deepcopy(self.good)
        t["events"][-1]["feature_count"] = 1
        result = flow.validate_trace(t)
        self.assertFalse(result["valid"])
        self.assertTrue(any("feature_count" in msg or "feature counts" in msg
                            for msg in result["issues"]))

    def test_invalid_order_missing_phase_and_nonpass_status(self):
        t = deepcopy(self.good)
        t["events"] = [event for event in t["events"] if event["phase"] != "RENDER"]
        self.assertIn("Missing required phase: RENDER", flow.validate_trace(t)["issues"])
        t = deepcopy(self.good)
        t["events"][2]["sequence"] = 1
        self.assertFalse(flow.validate_trace(t)["valid"])
        t = deepcopy(self.good)
        t["events"][-1]["status"] = "NOT RUN"
        self.assertFalse(flow.validate_trace(t)["valid"])

    def test_sensitive_fields_and_duplicate_phases_are_rejected(self):
        t = deepcopy(self.good)
        t["events"][0]["password"] = "not-an-actual-password"
        self.assertTrue(any("unsupported/sensitive" in msg for msg in
                            flow.validate_trace(t)["issues"]))
        t = deepcopy(self.good)
        t["events"].insert(1, deepcopy(t["events"][0]))
        self.assertTrue(any("duplicates a phase" in msg for msg in
                            flow.validate_trace(t)["issues"]))

    def test_lossy_counts_may_change_when_declared(self):
        t = deepcopy(self.good)
        t["operation"] = "filter"
        t["events"][-1]["feature_count"] = 1
        self.assertTrue(flow.validate_trace(t)["valid"])

    def test_cli_valid_and_malformed_inputs(self):
        program = str(ROOT / "scripts/gis_data_flow_diagnostics.py")
        result = subprocess.run([sys.executable, program, "--trace", str(EXAMPLE_TRACE)],
                                capture_output=True, text=True, timeout=20)
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertTrue(json.loads(result.stdout)["valid"])
        with tempfile.TemporaryDirectory() as tmp:
            file = Path(tmp) / "bad.json"
            file.write_text('{"schema_version":"1.0","schema_version":"1.0"}', encoding="utf-8")
            result = subprocess.run([sys.executable, program, "--trace", str(file)],
                                    capture_output=True, text=True, timeout=20)
            self.assertNotEqual(0, result.returncode)
            self.assertIn("Duplicate JSON key", result.stderr)


if __name__ == "__main__":
    unittest.main()
