# QGIS / PyQGIS development

Official documentation: https://docs.qgis.org/testing/en/docs/pyqgis_developer_cookbook/

## Pick the smallest integration

- **Processing algorithm** for one reproducible spatial operation, supporting processing models and batch execution.
- **Processing provider plugin** for a collection of algorithms.
- **Full Python plugin** when custom UI, docks or maps/canvas interactivity are necessary.
- **Standalone PyQGIS** only where the deployment and provider paths are controlled.

A plugin typically needs `metadata.txt`, initialization entrypoint, appropriate action and lifecycle management, and optional algorithm provider. QGIS versions may differ in Qt/PyQGIS APIs: pin one tested release.

### Reliability checks

- Use correct `QgsProcessingParameter*` definitions and `feedback` cancellation reporting.
- Confirm coordinate transforms with `QgsCoordinateTransform`, project transform context, and non-degree linear units for distance tools.
- Treat FeatureSource/Sink and field/geometry definitions explicitly; avoid changing a layer when a new output is expected.
- Register/unregister processing providers consistently; prevent duplicate menus/toolbars after hot reload.
- Run actual integration tests from the matching QGIS install.

Read: https://github.com/qgis/QGIS-Documentation/blob/master/docs/pyqgis_developer_cookbook/plugins/plugins.rst

**Starter:** `templates/qgis-processing-algorithm.py` (algorithm class; plugin/provider packaging to be added separately).