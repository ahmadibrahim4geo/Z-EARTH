"""Offline MCP/native capability matching regressions: no network or native host."""
import importlib.util
import json
import subprocess
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("mcp_tool_router", ROOT / "scripts/mcp_tool_router.py")
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)
SAMPLES = ROOT / "examples/mcp-tool-routing"

class CandidateRoutingTests(unittest.TestCase):
    def setUp(self):
        self.plan = json.loads((SAMPLES / "plan.example.json").read_text(encoding="utf-8"))
        self.inventory = json.loads((SAMPLES / "inventory.example.json").read_text(encoding="utf-8"))
        self.registry = json.loads((ROOT / "references/mcp-capability-registry.json").read_text(encoding="utf-8"))

    def test_selects_mcp_native_fallback_and_blocks_missing(self):
        result = mod.route_plan(self.plan, self.inventory, self.registry)
        self.assertEqual(result["blocked_requirements"], 1)
        self.assertEqual(result["task_routes"][0]["requirements"][0]["candidate_protocol"], "mcp")
        self.assertEqual(result["task_routes"][1]["requirements"][0]["candidate_protocol"], "native")
        self.assertEqual(result["task_routes"][1]["requirements"][1]["status"], "BLOCKED_NO_TOOL")
        self.assertFalse(result["can_execute"])
        self.assertFalse(result["inventory_authenticated"])

    def test_no_inventory_never_invents_mcp_server(self):
        result = mod.route_plan(self.plan, {"schema_version":"1.0","tools":[]}, self.registry)
        self.assertEqual(result["blocked_requirements"], 3)
        self.assertTrue(all(x["candidate_tool_id"] is None for row in result["task_routes"] for x in row["requirements"]))

    def test_rejects_undeclared_capabilities(self):
        self.inventory["tools"][0]["capabilities"] = ["filesystem.root.shell"]
        with self.assertRaises(mod.ContractError):
            mod.route_plan(self.plan, self.inventory, self.registry)

    def test_rejects_escalated_side_effects(self):
        self.plan["tasks"][0]["capability_requests"][0]["side_effect"] = "destructive"
        with self.assertRaises(mod.ContractError):
            mod.route_plan(self.plan, self.inventory, self.registry)

    def test_rejects_tool_ids_and_duplicate_inventory(self):
        self.inventory["tools"][0]["tool_id"] = "../shell"
        with self.assertRaises(mod.ContractError):
            mod.route_plan(self.plan, self.inventory, self.registry)
        self.inventory["tools"][0]["tool_id"] = self.inventory["tools"][1]["tool_id"]
        with self.assertRaises(mod.ContractError):
            mod.route_plan(self.plan, self.inventory, self.registry)

    def test_fallback_does_not_grant_execution(self):
        self.inventory["tools"][0]["available"] = False
        self.inventory["tools"].append({"tool_id":"alt.native","protocol":"native","available":True,
            "capabilities":["github.repository.read"],"side_effects":["read"]})
        result = mod.route_plan(self.plan, self.inventory, self.registry)
        self.assertEqual(result["task_routes"][0]["requirements"][0]["candidate_tool_id"], "alt.native")
        self.assertFalse(result["task_routes"][0]["requirements"][0]["host_permission_verified"])

    def test_cli_reports_blocked_requirements_without_invocations(self):
        run = subprocess.run([sys.executable, str(ROOT / "scripts/mcp_tool_router.py"),
            "--plan", str(SAMPLES / "plan.example.json"),
            "--inventory", str(SAMPLES / "inventory.example.json")],
            capture_output=True, text=True, check=False)
        self.assertEqual(run.returncode, 1, run.stderr)
        self.assertIn("BLOCKED_NO_TOOL", run.stdout)
        self.assertIn('"can_execute": false', run.stdout)

    def test_integrated_intent_workflow_keeps_execution_false(self):
        run = subprocess.run([sys.executable, str(ROOT / "scripts/intent_router.py"),
            "--plan", str(SAMPLES / "plan.example.json"), "--workflow",
            "--mcp-inventory", str(SAMPLES / "inventory.example.json"), "--format", "json"],
            capture_output=True, text=True, check=False)
        self.assertEqual(run.returncode, 0, run.stderr)
        blueprint = json.loads(run.stdout)
        self.assertTrue(blueprint["valid"], blueprint["errors"])
        self.assertFalse(blueprint["can_execute"])
        self.assertEqual(blueprint["tool_route_preview"]["blocked_requirements"], 1)

if __name__ == "__main__":
    unittest.main()

