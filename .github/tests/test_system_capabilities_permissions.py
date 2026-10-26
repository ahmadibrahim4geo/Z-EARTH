"""Offline candidate registry and permission-contract tests, NOT native device tests."""
import importlib.util
import json
import subprocess
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("system_capability_check", ROOT / "scripts/system_capability_check.py")
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)


class SystemCapabilityTests(unittest.TestCase):
    def test_registry_candidate_only(self):
        data = mod.load_registry()
        self.assertGreaterEqual(len(data["adapters"]), 10)
        self.assertEqual(len({a["id"] for a in data["adapters"]}), len(data["adapters"]))
        self.assertTrue(all(a["review_status"] == "candidate_unverified" for a in data["adapters"]))

    def test_location_not_granted_from_catalog(self):
        obj = mod.plan("windows", ["location", "time", "location"])
        self.assertEqual(len(obj["capabilities"]), 2)
        loc, clock = obj["capabilities"]
        self.assertEqual(loc["permission_status"], "NOT_REQUESTED")
        self.assertEqual(loc["availability"], "CANDIDATE")
        self.assertFalse(loc["native_verified"])
        self.assertEqual(loc["test_status"], "NOT RUN")
        self.assertEqual(clock["permission_status"], "NOT_REQUIRED")
        self.assertNotIn("latitude", json.dumps(obj))

    def test_unknown_no_fake_hardware(self):
        x = mod.plan("unknown", ["bluetooth"])["capabilities"][0]
        self.assertEqual(x["availability"], "UNASSESSED")
        self.assertEqual(x["candidate_adapters"], [])

    def test_unknown_requests_fail(self):
        for platform, caps in [("wrong", ["time"]), ("windows", []), ("windows", ["administrator"])]:
            with self.subTest(platform=platform), self.assertRaises(ValueError):
                mod.plan(platform, caps)

    def test_cli_output_is_not_a_permission_grant(self):
        run = subprocess.run([sys.executable, str(ROOT / "scripts/system_capability_check.py"),
                              "--platform", "browser", "--capability", "camera"],
                             capture_output=True, text=True, timeout=15)
        self.assertEqual(run.returncode, 0, run.stderr)
        cap = json.loads(run.stdout)["capabilities"][0]
        self.assertEqual(cap["permission_status"], "NOT_REQUESTED")
        self.assertFalse(cap["native_verified"])

    def test_router_and_wizard_link(self):
        slug = "system-capabilities-and-permissions"
        for path in ("skills/gis-development/SKILL.md", "skills/gis-device-context/SKILL.md",
                     "project-wizard.html", "references/repository-manifest.json"):
            self.assertIn(slug, (ROOT / path).read_text(encoding="utf-8"), path)


if __name__ == "__main__":
    unittest.main()
