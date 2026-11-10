"""Offline behavior tests; no provider, generator or native UI claims."""
import copy
import hashlib
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load_script(name):
    spec = importlib.util.spec_from_file_location(name, ROOT / "scripts" / (name + ".py"))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


assets = load_script("prepare_visual_asset")
router = load_script("intent_router")
META = {
    "asset_id": "test-mark", "role": "logo", "representation": "brand-mark",
    "source_page": "https://example.org/synthetic-fixture",
    "license_url": "https://example.org/synthetic-fixture-terms",
    "creator": "Test fixture author", "license": "Synthetic fixture only",
    "attribution": "Unit test fixture, not an acquired brand asset",
    "alt": "Synthetic red rectangle", "intended_use": "Offline unit test",
    "source_verified": True, "license_verified": True,
}


class VisualMetadataTests(unittest.TestCase):
    def test_bilingual_request_routes_include_assets(self):
        for prompt in ("Find official logos for provider cards",
                       "Create thumbnails for terrain models",
                       "اعمل سامبنيل وصورة لكل اختيار", "ابحث عن شعارات وصور توضيحية"):
            self.assertIn("visual-asset-discovery-integration",
                          router.route_hints(prompt)["suggested_skills"])

    def test_reject_unreviewed_license_and_unsafe_identity(self):
        for field, value in (("license_verified", False), ("source_verified", "true"),
                             ("license", "unknown"), ("asset_id", "../escape"),
                             ("source_page", "https://user:secret@example.org/asset")):
            item = dict(META, **{field: value})
            self.assertTrue(assets.metadata_errors(item), field)

    def test_scientific_preview_requires_actual_data_reference(self):
        item = dict(META, role="scientific-preview", representation="illustrative")
        self.assertTrue(assets.metadata_errors(item))
        item.update(representation="actual-preview", data_reference="Synthetic DEM test and render context")
        self.assertEqual([], assets.metadata_errors(item))

    def test_generated_asset_requires_real_tool_reference(self):
        item = dict(META, role="illustration", representation="generated")
        self.assertTrue(assets.metadata_errors(item))
        item.update(generator="Test-only stub declaration", generation_reference="No production generation")
        self.assertEqual([], assets.metadata_errors(item))

    def test_duplicate_json_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            json.loads('{"asset_id":"a","asset_id":"b"}', object_pairs_hook=assets.unique_pairs)


try:
    from PIL import Image
except ImportError:
    Image = None


@unittest.skipIf(Image is None, "Pillow optional; raster runtime not installed")
class VisualPreparationTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.source = self.root / "original.png"
        Image.new("RGBA", (256, 128), (220, 10, 20, 255)).save(self.source)
        self.original = self.source.read_bytes()

    def test_logo_padding_alpha_dimensions_and_hash_readback(self):
        target = self.root / "new-assets"
        record = assets.prepare(self.source, META, target)
        self.assertEqual(2, len(record["variants"]))
        self.assertEqual(hashlib.sha256(self.original).hexdigest(), record["original_sha256"])
        for variant in record["variants"]:
            path = target / variant["path"]
            with Image.open(path) as pic:
                self.assertEqual((variant["width"], variant["height"]), pic.size)
                self.assertEqual(0, pic.getpixel((0, 0))[3])
                self.assertEqual((220, 10, 20, 255), pic.getpixel((pic.width // 2, pic.height // 2)))
            self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest(), variant["sha256"])
        self.assertEqual(record, json.loads((target / "asset.json").read_text()))
        self.assertEqual(self.original, self.source.read_bytes())

    def test_logo_crop_and_existing_output_leave_originals_untouched(self):
        target = self.root / "assets"
        with self.assertRaisesRegex(ValueError, "contain"):
            assets.prepare(self.source, META, target, fit="cover")
        self.assertFalse(target.exists())
        target.mkdir()
        sentinel = target / "keep.txt"
        sentinel.write_text("keep")
        with self.assertRaisesRegex(ValueError, "already exists"):
            assets.prepare(self.source, META, target)
        self.assertEqual("keep", sentinel.read_text())
        self.assertEqual(self.original, self.source.read_bytes())

    def test_corrupt_input_and_invalid_dimensions_create_no_output(self):
        bad = self.root / "bad.png"
        bad.write_bytes(b"not an image")
        target = self.root / "output"
        with self.assertRaises((OSError, ValueError)):
            assets.prepare(bad, META, target)
        with self.assertRaises(ValueError):
            assets.prepare(self.source, META, target, width=0)
        self.assertFalse(target.exists())

    def test_cover_photo_is_cropped_to_requested_rectangle(self):
        item = dict(META, role="photo", representation="photograph")
        record = assets.prepare(self.source, item, self.root / "photo",
                                width=80, height=40, scales=(1,), fit="cover")
        self.assertEqual((80, 40), (record["variants"][0]["width"], record["variants"][0]["height"]))

    def test_cover_cannot_upscale(self):
        item = dict(META, role="thumbnail", representation="illustrative")
        with self.assertRaisesRegex(ValueError, "upscale"):
            assets.prepare(self.source, item, self.root / "huge", width=512, height=512, fit="cover")
        self.assertFalse((self.root / "huge").exists())

    def test_scientific_preview_cannot_crop(self):
        item = copy.deepcopy(META)
        item.update(role="scientific-preview", representation="actual-preview",
                    data_reference="Synthetic data only")
        with self.assertRaisesRegex(ValueError, "contain"):
            assets.prepare(self.source, item, self.root / "science", fit="cover")


if __name__ == "__main__":
    unittest.main()

