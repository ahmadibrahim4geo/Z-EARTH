"""KML preservation fixture contract tests. These are NOT native renderer tests."""
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "examples" / "kml-kmz" / "styled-roundtrip.kml"
K = "{http://www.opengis.net/kml/2.2}"
GX = "{http://www.google.com/kml/ext/2.2}"


class KmlPreservationFixtureTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.root = ET.parse(SOURCE).getroot()

    def test_has_expected_hierarchy_and_named_features(self):
        doc = self.root.find(K + "Document")
        self.assertEqual(doc.attrib["id"], "demoDocument")
        folder = doc.find(K + "Folder")
        self.assertEqual(folder.attrib["id"], "sourceFolder")
        self.assertEqual(folder.findtext(K + "visibility"), "1")
        self.assertEqual(
            [x.attrib["id"] for x in folder.findall(K + "Placemark")],
            ["samplePoint", "sampleLine", "samplePolygon", "sampleTrack"],
        )

    def test_style_map_is_not_lost_and_color_channels_are_kml_order(self):
        doc = self.root.find(K + "Document")
        styles = {x.attrib["id"]: x for x in doc.findall(K + "Style")}
        self.assertEqual(set(styles), {"normalStyle", "highlightStyle"})
        normal = styles["normalStyle"]
        self.assertEqual(normal.findtext(K + "IconStyle/" + K + "color"), "ff0000ff")
        self.assertEqual(normal.findtext(K + "PolyStyle/" + K + "color"), "7fff0000")
        self.assertEqual(normal.find(K + "IconStyle/" + K + "hotSpot").attrib["xunits"], "fraction")
        style_map = doc.find(K + "StyleMap")
        pairs = {x.findtext(K + "key"): x.findtext(K + "styleUrl") for x in style_map.findall(K + "Pair")}
        self.assertEqual(pairs, {"normal": "#normalStyle", "highlight": "#highlightStyle"})

    def test_point_data_altitude_and_description(self):
        point = self.root.find(".//" + K + "Placemark[@id='samplePoint']")
        self.assertEqual(point.findtext(K + "styleUrl"), "#twoStates")
        self.assertEqual(point.findtext(K + "TimeStamp/" + K + "when"), "2026-10-25T09:00:00Z")
        self.assertEqual(point.findtext(K + "Point/" + K + "altitudeMode"), "relativeToGround")
        self.assertEqual(point.findtext(K + "Point/" + K + "coordinates"), "31.24,30.04,20")
        self.assertEqual(point.find(K + "LookAt").findtext(K + "longitude"), "31.24")
        self.assertEqual(point.find(K + "ExtendedData/" + K + "Data").attrib["name"], "station_code")
        self.assertIn("Safe original description", point.findtext(K + "description"))

    def test_polygon_has_hole_and_gx_track_has_ordered_pairs(self):
        polygon = self.root.find(".//" + K + "Placemark[@id='samplePolygon']/" + K + "Polygon")
        self.assertEqual(len(polygon.findall(K + "innerBoundaryIs")), 1)
        self.assertEqual(len(polygon.findall(K + "outerBoundaryIs")), 1)
        track = self.root.find(".//" + K + "Placemark[@id='sampleTrack']/" + GX + "Track")
        times = [x.text for x in track.findall(K + "when")]
        coords = [x.text for x in track.findall(GX + "coord")]
        self.assertEqual(len(times), len(coords))
        self.assertEqual(coords, ["31.24 30.04 100", "31.25 30.05 110"])

    def test_unique_feature_style_ids_and_resolved_internal_style_urls(self):
        ids = [e.attrib["id"] for e in self.root.iter() if "id" in e.attrib]
        self.assertEqual(len(ids), len(set(ids)))
        for tag in self.root.iter(K + "styleUrl"):
            self.assertTrue(tag.text.startswith("#"))
            self.assertIn(tag.text[1:], ids)


if __name__ == "__main__":
    unittest.main()
