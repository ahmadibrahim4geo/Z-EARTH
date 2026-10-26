"""Offline regression checks for repository-wide orchestration documentation.

These checks verify static integration only; they do not prove host permissions,
agent routing behavior, native GIS execution or installer acceptance.
"""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
CONTRACT = ROOT / "references" / "repository-wide-skill-orchestration.md"


class RepositoryOrchestrationContractTests(unittest.TestCase):
    def test_contract_linked_from_all_entrypoints(self):
        entrypoints = (
            "AGENTS.md",
            "skills/gis-development/SKILL.md",
            "skills/prompt-intent-orchestrator/SKILL.md",
        )
        for relpath in entrypoints:
            with self.subTest(entrypoint=relpath):
                text = (ROOT / relpath).read_text(encoding="utf-8")
                self.assertIn("repository-wide-skill-orchestration.md", text)

    def test_explicit_reviewed_state_transitions(self):
        text = CONTRACT.read_text(encoding="utf-8")
        for state in (
            "RECEIVED", "INTERPRETED", "DRAFTED", "AWAITING_REVIEW",
            "APPROVED", "ROUTED", "PREFLIGHT", "EXECUTING",
            "VERIFYING", "REPORTED", "BLOCKED", "NEEDS_REVISION",
        ):
            with self.subTest(state=state):
                self.assertIn(state, text)
        self.assertLess(text.index("AWAITING_REVIEW"), text.index("APPROVED"))
        self.assertLess(text.index("APPROVED"), text.index("EXECUTING"))

    def test_cross_skill_envelope_and_provenance(self):
        text = CONTRACT.read_text(encoding="utf-8")
        for key in (
            "original_prompt", "request_id", "plan_revision", "approval_scope",
            "task_nodes", "dependency_edges", "specialist_owner",
            "source_provenance", "acceptance_tests", "rollback_plan",
        ):
            with self.subTest(field=key):
                self.assertIn(key, text)

    def test_owner_security_and_evidence_rules(self):
        text = CONTRACT.read_text(encoding="utf-8")
        for term in (
            "Security Guardian", "GIS Development Router", "Development Journal",
            "one owner", "T0", "T1", "T2", "T3", "T4", "NOT RUN",
        ):
            with self.subTest(term=term):
                self.assertIn(term.lower(), text.lower())
        self.assertIn("Markdown cannot technically intercept", text)

    def test_multiple_delivery_targets_and_scope_change(self):
        text = CONTRACT.read_text(encoding="utf-8")
        for term in (
            "ArcGIS Pro", "standalone GIS app", "web map",
            "DEM", "spatial database", "Change the engine after plan approval",
        ):
            with self.subTest(scenario=term):
                self.assertIn(term.lower(), text.lower())


if __name__ == "__main__":
    unittest.main()
