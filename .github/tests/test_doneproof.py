"""DoneProof structural checks only; no agent runtime or provider evidence is exercised."""
import copy
import importlib.util
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("doneproof", ROOT / "scripts/verify_completion.py")
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)


class DoneProofTests(unittest.TestCase):
    def setUp(self):
        self.report = {
            "schema_version": "1.0", "task_id": "example", "revision": "r2",
            "overall_status": "verified", "criteria": [{
                "id": "C1", "statement": "Readback verified", "status": "verified",
                "evidence": [{"type": "artifact_readback", "reference": "run/readback.log",
                              "observed": "Expected content observed", "result": "pass",
                              "revision": "r2"}]
            }]
        }

    def rejected(self):
        self.assertTrue(mod.validate(self.report))

    def test_valid_verified_shape(self):
        self.assertEqual(mod.validate(self.report), [])

    def test_partial_sample_shape(self):
        example = json.loads((ROOT / "templates/doneproof.example.json").read_text())
        self.assertEqual(mod.validate(example), [])
        self.assertEqual(example["overall_status"], "partial")

    def test_missing_evidence(self):
        self.report["criteria"][0]["evidence"] = []
        self.rejected()

    def test_stale_revision(self):
        self.report["criteria"][0]["evidence"][0]["revision"] = "r1"
        self.rejected()

    def test_agent_assertion_rejected(self):
        self.report["criteria"][0]["evidence"][0]["type"] = "agent_assertion"
        self.rejected()

    def test_failed_observation_not_verified(self):
        self.report["criteria"][0]["evidence"][0]["result"] = "fail"
        self.rejected()

    def test_missing_criterion_blocks_verified(self):
        self.report["criteria"].append({"id": "C2", "statement": "Published",
                                        "status": "not_run", "reason": "No provider", "evidence": []})
        self.rejected()

    def test_duplicate_id(self):
        self.report["criteria"].append(copy.deepcopy(self.report["criteria"][0]))
        self.rejected()

    def test_failed_status_requires_failure(self):
        self.report["overall_status"] = "failed"
        self.report["criteria"][0]["status"] = "failed"
        self.rejected()

    def test_partial_and_blocked(self):
        self.report["overall_status"] = "partial"
        self.report["criteria"][0].update(status="partial", reason="Only static checks")
        self.report["criteria"].append({"id": "C2", "statement": "Native GUI",
                                        "status": "blocked", "reason": "No Windows host", "evidence": []})
        self.assertEqual(mod.validate(self.report), [])

    def test_not_run_with_evidence(self):
        self.report["overall_status"] = "not_run"
        self.report["criteria"][0].update(status="not_run", reason="Not executed")
        self.rejected()

    def test_malformed_types_cannot_crash_validator(self):
        self.report["criteria"][0]["status"] = []
        self.assertTrue(mod.validate(self.report))
        self.report["criteria"][0]["status"] = "verified"
        self.report["criteria"][0]["evidence"][0]["type"] = {}
        self.assertTrue(mod.validate(self.report))
        self.report["overall_status"] = []
        self.assertTrue(mod.validate(self.report))

    def test_entrypoints(self):
        for rel in ("AGENTS.md", "skills/gis-development/SKILL.md",
                    "references/repository-wide-skill-orchestration.md"):
            with self.subTest(rel=rel):
                self.assertIn("completion-evidence", (ROOT / rel).read_text())

    def test_upstream_review(self):
        text = (ROOT / "references/skill-creator-superpowers-review.md").read_text()
        self.assertIn("openai/skills", text)
        self.assertIn("obra/superpowers", text)


if __name__ == "__main__":
    unittest.main()
