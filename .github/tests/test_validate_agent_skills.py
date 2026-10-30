"""Windows checkout regression tests for the pinned snapshot validator."""
import importlib.util
import tempfile
import unittest
from pathlib import Path


REPO = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location(
    "validate_agent_skills", REPO / "scripts" / "validate-agent-skills.py"
)
VALIDATOR = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(VALIDATOR)


class ValidateAgentSkillsTests(unittest.TestCase):
    def test_git_blob_hash_is_stable_across_lf_and_crlf_checkouts(self):
        with tempfile.TemporaryDirectory(dir=REPO) as folder:
            lf = Path(folder) / "lf.md"
            crlf = Path(folder) / "crlf.md"
            lf.write_bytes(b"line one\nline two\n")
            crlf.write_bytes(b"line one\r\nline two\r\n")
            self.assertEqual(VALIDATOR.git_blob_sha(lf), VALIDATOR.git_blob_sha(crlf))

    def test_git_blob_hash_still_detects_content_changes(self):
        with tempfile.TemporaryDirectory(dir=REPO) as folder:
            first = Path(folder) / "first.md"
            changed = Path(folder) / "changed.md"
            first.write_bytes(b"safe content\n")
            changed.write_bytes(b"changed content\n")
            self.assertNotEqual(VALIDATOR.git_blob_sha(first), VALIDATOR.git_blob_sha(changed))


if __name__ == "__main__":
    unittest.main()
