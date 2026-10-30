import importlib.util
import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts" / "ge_functional_spec_backlog.py"
SPEC = importlib.util.spec_from_file_location("ge_functional_spec_backlog", SCRIPT)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(MODULE)


class FunctionalSpecBacklogTests(unittest.TestCase):
    def test_parser_accepts_feature_rows_and_ignores_status_legend(self):
        text = """
| `PARTIAL` | description | action |
| `GRID-001` | Master Grid | behavior | acceptance | `NEEDS_T3_AUDIT` |
| `TOOLS-009` | Flight | behavior | acceptance | `NEEDS_T3_AUDIT` |
"""
        rows = MODULE.parse_feature_rows(text)
        self.assertEqual(["GRID-001", "TOOLS-009"], [row["id"] for row in rows])
        self.assertTrue(all(row["current_status"] == "NEEDS_AUDIT" for row in rows))

    def test_duplicate_feature_ids_are_rejected(self):
        row = "| `GRID-001` | Grid | behavior | acceptance | `NEEDS_T3_AUDIT` |"
        with self.assertRaisesRegex(ValueError, "duplicate feature ID"):
            MODULE.parse_feature_rows(row + "\n" + row)

    def test_overrides_validate_ids_and_statuses(self):
        rows = MODULE.parse_feature_rows(
            "| `FILE-005` | Save Image | behavior | acceptance | `NEEDS_T3_AUDIT` |"
        )
        MODULE.apply_overrides(rows, {"FILE-005": {"current_status": "PARTIAL"}})
        self.assertEqual("PARTIAL", rows[0]["current_status"])
        with self.assertRaisesRegex(ValueError, "override IDs not found"):
            MODULE.apply_overrides(rows, {"BAD-999": {"current_status": "PARTIAL"}})
        with self.assertRaisesRegex(ValueError, "unsupported current_status"):
            MODULE.apply_overrides(rows, {"FILE-005": {"current_status": "DONE"}})

    def test_payload_can_be_serialized_with_arabic(self):
        rows = MODULE.parse_feature_rows(
            "| `GRID-001` | الشبكة | سلوك | قبول | `NEEDS_T3_AUDIT` |"
        )
        encoded = json.dumps({"features": rows}, ensure_ascii=False).encode("utf-8")
        self.assertIn("الشبكة", encoded.decode("utf-8"))


if __name__ == "__main__":
    unittest.main()
