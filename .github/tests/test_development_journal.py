"""Offline regression tests for durable, linked, non-destructive project journal."""
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("development_journal", ROOT / "scripts" / "development_journal.py")
journal = importlib.util.module_from_spec(spec)
spec.loader.exec_module(journal)


class DevelopmentJournalTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        journal.init(self.root)

    def record(self, **options):
        args = dict(kind="request", status="proposed", summary="طلب المستخدم",
                    details="استيراد KML", task_id="T1")
        args.update(options)
        return journal.add(self.root, **args)

    def test_resumption_revision_cancellation(self):
        self.record()
        self.record(kind="implementation", status="completed", summary="Create KML import",
                    details="Implementation tested", references=[1],
                    changed_files=["src/kml.py"], tests=["unittest"], test_result="passed")
        self.record(kind="revision", summary="Change icon options",
                    details="New user-requested change", references=[2])
        self.record(kind="cancellation", status="cancelled", summary="Cancel icon edit",
                    details="User withdrew prior edit", references=[3])
        rows = journal.verify(self.root)
        self.assertEqual([r["sequence"] for r in rows], [1, 2, 3, 4])
        self.assertEqual(rows[3]["references"], [3])
        self.assertIn("related: #0003", (self.root / journal.FOLDER / "STATE.md").read_text())
        journal.init(self.root)
        self.assertEqual(len(journal.verify(self.root)), 4)

    def test_rollback_cannot_delete_original(self):
        self.record()
        self.record(kind="rollback", status="proposed", summary="Revert change",
                    details="User approval needed", references=[1])
        self.assertTrue((self.root / journal.FOLDER / "records/0001.json").exists())

    def test_bad_links_and_false_completion(self):
        with self.assertRaises(ValueError):
            self.record(kind="revision", references=[3])
        with self.assertRaises(ValueError):
            self.record(kind="revision")
        with self.assertRaises(ValueError):
            self.record(kind="implementation", status="completed")
        self.assertEqual(journal.verify(self.root), [])

    def test_unsafe_paths(self):
        for path in ("../secret", "/etc/passwd", "C:\\private", "a/../b",
                     ".agent-development-log/index", "a//b"):
            with self.subTest(path=path), self.assertRaises(ValueError):
                self.record(changed_files=[path])
        self.assertEqual(journal.verify(self.root), [])

    def test_derived_repair_and_canonical_tamper_detection(self):
        self.record()
        folder = self.root / journal.FOLDER
        (folder / "reports/0001.md").write_text("wrong")
        with self.assertRaises(ValueError):
            journal.verify(self.root)
        journal.repair(self.root)
        self.assertEqual(len(journal.verify(self.root)), 1)
        record = folder / "records/0001.json"
        data = json.loads(record.read_text())
        data["summary"] = "forged"
        record.write_text(json.dumps(data))
        with self.assertRaises(ValueError):
            journal.verify(self.root)
        with self.assertRaises(ValueError):
            journal.repair(self.root)

    def test_init_recovers_missing_state(self):
        self.record()
        (self.root / journal.FOLDER / "STATE.md").unlink()
        journal.init(self.root)
        self.assertEqual(len(journal.verify(self.root)), 1)

    def test_writer_lock_refuses_collision(self):
        with journal._locked(self.root / journal.FOLDER):
            with self.assertRaises(ValueError):
                self.record()

    def test_symlink_directory_refused(self):
        folder = self.root / journal.FOLDER
        (folder / "reports").rename(folder / "old-reports")
        (folder / "reports").symlink_to(folder / "old-reports", target_is_directory=True)
        with self.assertRaises(ValueError):
            self.record()


if __name__ == "__main__":
    unittest.main()