"""Contract tests for reviewed reference analysis routing (no agent launch)."""
import json
import subprocess
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

class ReferenceImplementationRoutingTests(unittest.TestCase):
    def test_clean_room_arabic_intent(self):
        proc = subprocess.run(
            [sys.executable, str(ROOT / "scripts/intent_router.py"),
             "--text", "عايز تحليل برنامج مرجعي ثم تطوير زيرث بنفس الوظائف", "--format", "json"],
            capture_output=True, text=True, cwd=ROOT, check=True)
        data = json.loads(proc.stdout)
        self.assertIn("clean-room-reference-implementation", data["suggested_skills"])
        self.assertIn("gis-security-guardian", data["suggested_skills"])
        self.assertTrue(data["requires_semantic_review"])
        self.assertEqual("not_requested", data["approval"])

    def test_clean_room_english_intent(self):
        proc = subprocess.run(
            [sys.executable, str(ROOT / "scripts/intent_router.py"),
             "--text", "Reference software feature parity for desktop globe", "--format", "json"],
            capture_output=True, text=True, cwd=ROOT, check=True)
        data = json.loads(proc.stdout)
        self.assertIn("clean-room-reference-implementation", data["suggested_skills"])
        self.assertFalse(any(x.endswith("agent_started") for x in data))

    def test_skill_and_master_router_links(self):
        master = (ROOT / "skills/gis-development/SKILL.md").read_text(encoding="utf-8")
        reference = (ROOT / "skills/clean-room-reference-implementation/SKILL.md").read_text(encoding="utf-8")
        self.assertIn("clean-room-reference-implementation/SKILL.md", master)
        self.assertIn("license: MIT", reference)
        self.assertIn("NOT", reference.upper())  # Distinguish unverified evidence clearly.

if __name__ == "__main__":
    unittest.main()

