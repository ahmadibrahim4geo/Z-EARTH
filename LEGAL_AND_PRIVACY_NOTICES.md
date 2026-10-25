# Z EARTH Desktop GIS — Legal, Privacy & Security Notices

**Product Name:** Z EARTH Desktop GIS  
**Product Version:** 1.0.0 (Release)  
**Lead Architect & Developer:** Ahmad Ibrahim  
**Project Root:** `D:\My Software\Z EARTH`  
**License:** Open-Source Software (GPLv3 / Independent)

---

## 1. Clean Room Development & Intellectual Property Disclosures

1. **Independent Architecture:**
   Z EARTH Desktop GIS is an original, independently architected geographic information system and 3D globe visualization platform designed and implemented from first principles.
2. **Proprietary Software Non-Infringement:**
   Z EARTH Desktop GIS contains **zero proprietary code, zero private protocols, zero proprietary binary assets, and zero decompiled source routines** from Google Earth, Google Earth Pro, Esri ArcGIS, or any other proprietary GIS products.
3. **Clean-Room Compatibility:**
   Compatibility with open geospatial standards (OGC KML 2.2, KMZ, GeoJSON, ESRI Shapefile, OGC GeoPackage, WMS, WMTS, XYZ) is achieved solely through adherence to published open standards specifications and clean-room engineering.
4. **Trademarks:**
   "Google Earth" is a trademark of Google LLC. "ArcGIS" is a trademark of Environmental Systems Research Institute, Inc. (Esri). These names are referenced solely for comparative descriptive purposes under nominative fair use.

---

## 2. Privacy Policy & Local-First Architectural Posture

Z EARTH Desktop GIS is built from the ground up on strict **Privacy-by-Design** and **Local-First** principles:

1. **Zero Unsolicited Network Calls:**
   Upon launching the application, Z EARTH Desktop GIS initiates **zero background connections to third-party tracking services, analytics telemetry, or ad networks**.
2. **Default Offline Operation:**
   The application features a built-in vector globe basemap (`Natural Earth Vector`) and WGS84 Ellipsoidal terrain model that function entirely offline without requiring internet access or cloud services.
3. **Privacy-Preserving Geolocation:**
   Z EARTH Desktop GIS **never queries external IP geolocation databases by default**. The default startup camera frames Egypt and the Nile Valley within a continental perspective using local configuration presets. Any remote geolocation requires explicit user opt-in (`EnableIpGeolocation = true`).
4. **Local Data Persistence:**
   All user projects (`.zearth`), saved scenes, places hierarchies (`My Places`), custom coordinate systems, and cached layers are stored strictly on the local machine in standard open formats. No user data is transmitted to external servers.

---

## 3. Security Architecture & Threat Mitigation

1. **Anti-ZipSlip Archive Defense:**
   The KMZ and `.zearth` extraction engines enforce strict canonical path validation, preventing directory traversal vulnerabilities (ZipSlip) when unpacking compressed archives.
2. **Decompression Bomb Protection:**
   Archive readers enforce strict maximum file count and uncompressed size boundaries (100 MB ceiling per layer) to protect system resources against denial-of-service attempts.
3. **Sandboxed Web Viewport:**
   The rendering engine operates inside a sandboxed Microsoft Edge WebView2 control with dedicated user data directory isolation, disabled file system execution, and strict bidirectional message typing.
4. **Unhandled Exception Containment:**
   All background tasks, terrain loading errors, and projection exceptions are captured gracefully via localized event handlers, preventing process crashes or data corruption.
