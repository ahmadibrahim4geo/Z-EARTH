"""Mobile GIS skill integration and offline reference contract tests (T0/T1 only).

Requires no account, emulator or SDK. Native Android/iOS T2-T4 remain NOT RUN.
"""
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
MODULE = ROOT / "examples/mobile-gis/offline_sync_contract.py"
spec = importlib.util.spec_from_file_location("mobile_gis_sync_contract", MODULE)
sync = importlib.util.module_from_spec(spec)
import sys
sys.modules[spec.name] = sync
spec.loader.exec_module(sync)


class OfflineSyncContractTests(unittest.TestCase):
    def setUp(self):
        self.server = sync.MemoryServer()
        self.outbox = sync.Outbox()
        self.create = sync.Edit("op-1", "point-001", "create", None, {"name": "القاهرة", "epsg": 4326})

    def test_unauthenticated_and_airplane_mode_preserve_pending(self):
        self.outbox.queue(self.create)
        self.assertEqual({}, self.outbox.flush(self.server, connected=False, authenticated=True))
        self.assertEqual({}, self.outbox.flush(self.server, connected=True, authenticated=False))
        self.assertEqual(1, len(self.outbox.pending))
        self.assertEqual({}, self.server.features)
        self.assertEqual({"op-1": 1}, self.outbox.flush(self.server, connected=True, authenticated=True))
        self.assertEqual({}, self.outbox.pending)

    def test_retry_deduplication_returns_same_ack(self):
        self.assertEqual(1, self.server.apply(self.create, connected=True, authenticated=True))
        self.assertEqual(1, self.server.apply(self.create, connected=True, authenticated=True))
        self.assertEqual(1, len(self.server.acknowledged))
        self.assertEqual(1, self.server.features["point-001"]["revision"])

    def test_changed_payload_must_not_reuse_op_id(self):
        self.server.apply(self.create, connected=True, authenticated=True)
        forged = sync.Edit("op-1", "point-001", "create", None, {"name": "other"})
        with self.assertRaises(ValueError):
            self.server.apply(forged, connected=True, authenticated=True)

    def test_revision_conflict_keeps_edit_and_preserves_server_record(self):
        self.server.apply(self.create, connected=True, authenticated=True)
        self.server.apply(sync.Edit("server-other", "point-001", "update", 1, {"name": "remote"}),
                          connected=True, authenticated=True)
        self.outbox.queue(sync.Edit("op-2", "point-001", "update", 1, {"name": "local"}))
        self.assertEqual({}, self.outbox.flush(self.server, connected=True, authenticated=True))
        self.assertIn("op-2", self.outbox.conflicts)
        self.assertEqual("local", self.outbox.conflicts["op-2"].fields["name"])
        self.assertEqual("remote", self.server.features["point-001"]["fields"]["name"])

    def test_tombstone_is_not_silent_physical_delete(self):
        self.server.apply(self.create, connected=True, authenticated=True)
        revision = self.server.apply(sync.Edit("op-3", "point-001", "delete", 1, {}),
                                     connected=True, authenticated=True)
        self.assertEqual(2, revision)
        self.assertTrue(self.server.features["point-001"]["deleted"])

    def test_invalid_revisions_and_ids_rejected(self):
        for value in ("bad", -1, 0, True):
            with self.subTest(value=value):
                with self.assertRaises(ValueError):
                    sync.Edit("a", "x", "update", value, {})
        with self.assertRaises(ValueError):
            sync.Edit("", "x", "create", None, {})
        with self.assertRaises(ValueError):
            sync.Edit("ok", "x", "rename", None, {})

    def test_queued_input_is_snapshot_not_mutable_source(self):
        payload = {"label": "original"}
        edit = sync.Edit("op-snap", "f1", "create", None, payload)
        self.outbox.queue(edit)
        payload["label"] = "mutated"
        self.assertEqual("original", self.outbox.pending["op-snap"].fields["label"])

    def test_failed_create_is_conflict_not_success(self):
        self.server.apply(self.create, connected=True, authenticated=True)
        self.outbox.queue(sync.Edit("op-duplicate", "point-001", "create", None, {}))
        self.assertEqual({}, self.outbox.flush(self.server, connected=True, authenticated=True))
        self.assertIn("op-duplicate", self.outbox.conflicts)


class MobileSkillIntegrationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.skill = (ROOT / "skills/gis-mobile-application-development/SKILL.md").read_text(encoding="utf-8")
        cls.wizard = (ROOT / "project-wizard.html").read_text(encoding="utf-8")
        cls.router = (ROOT / "skills/gis-development/SKILL.md").read_text(encoding="utf-8")
        cls.policy = (ROOT / "references/free-first-policy.md").read_text(encoding="utf-8")

    def test_skill_links_and_owner_are_present(self):
        self.assertIn("name: gis-mobile-application-development", self.skill)
        for name in ("mobile-gis-architecture.md", "mobile-gis-offline-sync.md",
                     "mobile-gis-authentication.md", "mobile-gis-device-test-matrix.md"):
            self.assertIn(name, self.skill)
            self.assertTrue((ROOT / "references" / name).is_file())
        for slug in ("gis-security-guardian", "gis-device-context", "system-capabilities-and-permissions",
                     "coordinate-reference-systems", "kml-kmz-engineering"):
            self.assertIn(slug, self.skill)
        self.assertIn("NOT RUN", self.skill)

    def test_mobile_routes_both_in_router_and_wizard(self):
        self.assertIn("gis-mobile-application-development/SKILL.md", self.router)
        self.assertIn("gis-mobile-application-development", self.wizard)
        self.assertIn('value="mobile"', self.wizard)
        self.assertIn('id="mobilePlatform"', self.wizard)
        self.assertIn('id="mobileNetwork"', self.wizard)
        self.assertIn('id="mobileSignIn"', self.wizard)
        self.assertIn("gis-mobile-application-development", self.policy)

    def test_manifest_and_catalog_mobile_entries(self):
        data = json.loads((ROOT / "references/repository-manifest.json").read_text(encoding="utf-8"))
        self.assertIn("gis-mobile-application-development", data["skill_ids"])
        self.assertEqual(len(list((ROOT / "skills").glob("*/SKILL.md"))), data["skill_count"])
        catalog = json.loads((ROOT / "references/project-catalog.json").read_text(encoding="utf-8"))
        for name in ("QField", "Mergin Maps Mobile", "MapLibre Native", "Mapsui"):
            entries = [item for item in catalog["projects"] if item["name"] == name]
            self.assertEqual(1, len(entries), name)
            self.assertEqual("gis-mobile-application-development", entries[0]["primary_skill"], name)

    def test_mobile_device_evidence_not_misrepresented(self):
        guide = (ROOT / "references/mobile-gis-device-test-matrix.md").read_text(encoding="utf-8")
        self.assertIn("NOT_RUN", guide)
        self.assertIn("Android", guide)
        self.assertIn("iOS", guide)
        self.assertIn("XCUITest", guide)
        self.assertIn("emulator", guide)


if __name__ == "__main__":
    unittest.main()
