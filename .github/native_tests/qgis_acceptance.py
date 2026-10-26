#!/usr/bin/env python3
"""Native QGIS smoke test, not generic Python CI. Requires a working PyQGIS host."""
import tempfile
from pathlib import Path


def run():
    from qgis.core import (QgsApplication, QgsVectorLayer, QgsFeature, QgsGeometry,
                           QgsPointXY, QgsVectorFileWriter, QgsProject)
    from qgis.PyQt.QtCore import QVariant
    from qgis.core import QgsField
    app = QgsApplication([], False)
    app.initQgis()
    try:
        with tempfile.TemporaryDirectory(prefix="gis_agent_qgis_") as tmp:
            layer = QgsVectorLayer("Point?crs=EPSG:4326", "source", "memory")
            if not layer.isValid():
                raise AssertionError("PyQGIS memory layer failed")
            layer.dataProvider().addAttributes([QgsField("label", QVariant.String)])
            layer.updateFields()
            for value, lat, lon in [("a", 30, 31), ("b", 30.1, 31.1)]:
                item = QgsFeature(layer.fields())
                item.setGeometry(QgsGeometry.fromPointXY(QgsPointXY(lon, lat)))
                item.setAttributes([value])
                if not layer.dataProvider().addFeatures([item])[0]:
                    raise AssertionError("Could not add synthetic point")
            output = str(Path(tmp) / "temporary.gpkg")
            options = QgsVectorFileWriter.SaveVectorOptions()
            options.driverName = "GPKG"
            result = QgsVectorFileWriter.writeAsVectorFormatV3(
                layer, output, QgsProject.instance().transformContext(), options)
            if result[0] != QgsVectorFileWriter.NoError:
                raise AssertionError("GeoPackage write error {}".format(result))
            check = QgsVectorLayer(output, "verify", "ogr")
            if not check.isValid() or check.featureCount() != 2 or check.crs().authid() != "EPSG:4326":
                raise AssertionError("Native QGIS GeoPackage re-read failed")
            print("Native QGIS temporary GeoPackage readback PASS; count=2")
    finally:
        app.exitQgis()


if __name__ == "__main__":
    run()
