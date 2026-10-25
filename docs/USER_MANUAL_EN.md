# Z EARTH Desktop GIS — Comprehensive User Manual (English)

**Product:** Z EARTH Desktop GIS  
**Version:** 1.0.0 (Release)  
**Lead Architect & Developer:** Ahmad Ibrahim  
**Project Root:** `D:\My Software\Z EARTH`

---

## Table of Contents
1. [Introduction & Architectural Highlights](#1-introduction--architectural-highlights)
2. [System Requirements & Installation](#2-system-requirements--installation)
3. [User Interface Overview](#3-user-interface-overview)
4. [3D Globe & 2D Map Navigation](#4-3d-globe--2d-map-navigation)
5. [Places & Project Persistence (.zearth)](#5-places--project-persistence-zearth)
6. [Coordinate Reference Systems (CRS) & Projections](#6-coordinate-reference-systems-crs--projections)
7. [Vector Drawing & Feature Editing](#7-vector-drawing--feature-editing)
8. [Precision Snapping & Geodesic Measurements](#8-precision-snapping--geodesic-measurements)
9. [Basemaps, Terrain & Custom Imagery](#9-basemaps-terrain--custom-imagery)
10. [Vector Datasets (GeoJSON, Shapefile, GeoPackage)](#10-vector-datasets-geojson-shapefile-geopackage)
11. [Bilingual UI & RTL Support](#11-bilingual-ui--rtl-support)
12. [Keyboard Accelerators & Shortcuts Reference](#12-keyboard-accelerators--shortcuts-reference)

---

## 1. Introduction & Architectural Highlights

**Z EARTH Desktop GIS** is an independent, open-source 3D globe and desktop Geographic Information System (GIS) designed as a modern, high-precision alternative to legacy virtual globes.

### Core Architectural Pillars
- **Hybrid Native Shell:** Built on Microsoft .NET 10 x64 and WPF (MVVM) with a professional neutral dark-gray interface (#252629 / #303236 / #22A8DE).
- **True Geographic Engine:** Powered by CesiumJS 1.146.0 running inside an isolated, hardware-accelerated Microsoft Edge WebView2 runtime.
- **Offline-First Resilience:** Functions completely offline out-of-the-box using bundled Natural Earth vector datasets and WGS84 ellipsoidal terrain.
- **High-Precision Geodesy:** Authoritative 4th-order Krüger $n$-series Transverse Mercator projections, Vincenty ellipsoidal geodesic distance, and authalic spherical polygon area algorithms.
- **Clean-Room Compliance:** 100% original, legally compliant code with zero proprietary assets or dependencies on Google Earth or ArcGIS.

---

## 2. System Requirements & Installation

### Minimum Requirements
- **Operating System:** Windows 10 (version 1809 or higher) / Windows 11 (64-bit).
- **Runtime:** Microsoft .NET 10.0 Desktop Runtime (x64) and Microsoft Edge WebView2 Evergreen Runtime.
- **Hardware:** Dual-core 2.0 GHz CPU, 4 GB RAM, DirectX 11 / OpenGL 3.0 capable GPU with WebGL2 support.
- **Disk Space:** ~250 MB free disk space.

### Installation Methods

#### Option A: Portable Standalone (No Installation Required)
1. Download `Z EARTH-v1.0.0-win-x64-portable.zip` from `dist/`.
2. Extract the archive into any preferred directory (e.g. `D:\Tools\ZEarth`).
3. Run `Z EARTH.exe`.

#### Option B: Windows Automated Installer (Shortcuts & File Associations)
1. Open PowerShell and run the installation script:
   ```powershell
   powershell -ExecutionPolicy Bypass -File scripts\install-zearth.ps1
   ```
2. The installer will:
   - Install application binaries to `%LocalAppData%\Programs\ZEarth`.
   - Create Start Menu and Desktop shortcuts with the official Z EARTH icon.
   - Register file extensions `.zearth`, `.kml`, and `.kmz` with Windows Shell.
   - Register the application in Windows **Installed Apps / Add or Remove Programs**.

#### Uninstallation
To cleanly remove Z EARTH Desktop GIS, run:
```powershell
powershell -ExecutionPolicy Bypass -File scripts\uninstall-zearth.ps1
```
Or uninstall via Windows Settings -> Apps -> Installed Apps -> **Z EARTH Desktop GIS**.

---

## 3. User Interface Overview

The Z EARTH interface is organized into five functional regions:

```
+-------------------------------------------------------------------------+
| [Z Icon] Quick Access Toolbar (New, Open, Save, Undo, Redo)  [_][O][X]  |
+-------------------------------------------------------------------------+
| Ribbon Tabs: [Project] [View] [Drawing] [Measure] [Layers] [Terrain]... |
+------------------+------------------------------------------------------+
| Sidebar (Left)   | Central 3D Globe / 2D Map Viewport                   |
| - Places Tree    | (CesiumJS 1.146.0 WebGL Engine)                      |
|   * My Places    |                                                      |
|   * Temporary    |                                                      |
|   * Saved Scenes |                                                      |
| - Layers Panel   |                                                      |
+------------------+------------------------------------------------------+
| Status Bar: Coordinates: 30.0444° N, 31.2357° E | Elev: 24m | Eye: 12km |
+-------------------------------------------------------------------------+
```

### 1. Title Bar & Quick Access Toolbar (QAT)
- **Approved Logo & Title:** Displays active project name, modification asterisk (`*`), and developer credit (`Ahmad Ibrahim`).
- **QAT Actions:** One-click shortcuts for New Project (`Ctrl+N`), Open (`Ctrl+O`), Save (`Ctrl+S`), Undo (`Ctrl+Z`), Redo (`Ctrl+Y`), and Sidebar Dock toggle (`Ctrl+Shift+D`).

### 2. Ribbon Bar
- **Tab 1: Project:** File management, KML/KMZ Import/Export, and Project Properties.
- **Tab 2: View:** Navigation presets, 2D/3D mode toggling, Extent History, and camera locks.
- **Tab 3: Drawing:** Placemark (Point), Path (Polyline), and Polygon authoring tools.
- **Tab 4: Measurement:** High-precision Geodesic Distance, Polygon Area, and Snapping configuration.
- **Tab 5: Layers:** Basemap gallery (Offline, OSM, OpenTopo, Dark) and custom imagery overlays.
- **Tab 6: Terrain:** Elevation models, vertical exaggeration slider (0.2x – 5.0x), and reset.
- **Tab 7: Tools:** Vector data ingestion (Shapefile, GeoPackage, GeoJSON), Bilingual Language toggle (English/Arabic), Coordinate Converter, and High-Resolution Snapshot export.
- **Tab 8: Help:** Diagnostics inspector and About dialog.

### 3. Sidebar
- Collapsible via `Ctrl+B` or ribbon button.
- Dockable to either the Left or Right side of the screen (`Ctrl+Shift+D`).
- Contains the **Places Tree** and **Layers** management tabs.

### 4. Telemetry Status Bar
Live updates displaying current crosshair cursor coordinates (formatted in active CRS), terrain surface elevation, camera eye altitude, compass heading, and spatial reference authority code.

---

## 4. 3D Globe & 2D Map Navigation

### Mouse Navigation
| Action | Gesture | Description |
| :--- | :--- | :--- |
| **Pan** | Left Click + Drag | Slides camera across geographic surface |
| **Rotate / Orbit** | Right Click + Drag | Orbits camera around focal point |
| **Zoom** | Scroll Wheel | Smooth logarithmic zoom in / out |
| **Tilt / Pitch** | Middle Click + Drag | Tilts perspective between top-down and horizon view |

### Navigation Controls
- **Home (`Home` key):** Smoothly flies camera to configured home preset (Default: Egypt & Nile Basin continent framing).
- **Full Extent (`Ctrl+Shift+E`):** Zooms out to frame the entire global sphere.
- **Previous Extent (`Alt+Left`):** Steps backward through 50-state camera history ring buffer.
- **Next Extent (`Alt+Right`):** Steps forward through camera history.
- **North Up (`N` key):** Smoothly resets azimuth heading to $0.00^\circ$ True North.
- **Reset Pitch (`U` key):** Smoothly resets camera pitch to $-90^\circ$ top-down nadir view.
- **2D / 3D Toggle:** Switches seamlessly between 3D spherical globe and 2D planar map projection without losing camera focus.

---

## 5. Places & Project Persistence (.zearth)

### Places Hierarchy
- **📁 My Places:** Permanent storage container automatically persisted to user profile.
- **📁 Temporary Places:** Working staging area for imported KML/KMZ, GeoJSON, and Shapefile files. Right-click any item and choose **"Save to My Places"** to permanently keep it.
- **🎬 Saved Scenes:** Bookmarks capturing full 6-DOF camera coordinates (Lat, Lon, Alt, Heading, Pitch, Roll). Double-click any scene to animate camera directly to that exact view.

### Project Persistence (.zearth)
- Projects are saved as `.zearth` files — standardized ZIP archives containing `manifest.json` metadata and SQLite/JSON spatial models.
- **Autosave Protection:** Automatically writes snapshot recovery files every 30 seconds. In the event of a sudden power loss, Z EARTH detects the recovery marker on next startup and prompts to restore work.
- **Atomic Writes:** File saves are written to a temporary staging file and atomically swapped, guaranteeing zero project corruption if disk space is exhausted.

---

## 6. Coordinate Reference Systems (CRS) & Projections

### Dynamic Coordinate Formatting
Click the coordinates indicator in the status bar or press the format toggle to cycle through:
1. **Decimal Degrees (DD):** `30.044400° N, 31.235700° E`
2. **Degrees Minutes Seconds (DMS):** `30° 02' 39.8" N, 31° 14' 08.5" E`
3. **Universal Transverse Mercator (UTM):** `36N 329900.0m E, 3325017.0m N`
4. **Military Grid Reference System (MGRS):** `36RUU2989925016` (precision-selectable from 1m to 10km)

### Regional & Egyptian National Projections
Z EARTH includes pre-configured, sub-millimeter accurate Krüger $n$-series Transverse Mercator transformation engines:
- **Global:** WGS 84 Geographic (`EPSG:4326`), WGS 84 3D (`EPSG:4979`), Web Mercator (`EPSG:3857`).
- **Egypt UTM Zones:** UTM zone 35N (`EPSG:32635` - Alexandria/Western Desert), UTM zone 36N (`EPSG:32636` - Cairo/Nile Delta/Sinai), UTM zone 37N (`EPSG:32637` - Red Sea).
- **Egyptian National Grids:** Egypt 1907 / Red Belt (`EPSG:22992`), Purple Belt (`EPSG:22993`), South Belt (`EPSG:22994`).

---

## 7. Vector Drawing & Feature Editing

Select the **Drawing** tab on the Ribbon to interactively digitize spatial features:

1. **Placemark (Point):** Click the **Add Placemark** button, then click anywhere on the globe. A dialog allows setting name, description, coordinates, and custom pin icon color.
2. **Path (Polyline):** Click **Draw Path**, then left-click successive vertices across the globe. Double-click or right-click to complete. Press `Esc` to cancel.
3. **Polygon with Holes:** Click **Draw Polygon**, define boundary vertices, and optionally subtract inner boundary rings to model complex land parcels or islands. Features include translucent accent fill and boundary outlines.

All drawing actions are integrated into the command history stack: press `Ctrl+Z` to undo any vertex or deletion, and `Ctrl+Y` to redo.

---

## 8. Precision Snapping & Geodesic Measurements

### Snapping Engine
Located in Ribbon Tab 4 (**Measurement**):
- **Screen-Space Snapping Tolerance:** Snaps within 12 pixels (configurable to 8px, 12px, 16px, or 24px).
- **Target Modes:** Vertex snapping (cyan indicator), Midpoint snapping (yellow indicator), and Perpendicular Edge nearest projection (orange indicator).
- Operates in real-time using an internal spatial R-tree index.

### Geodesic Distance & Area
- **Geodesic Distance:** Uses rigorous WGS84 ellipsoidal Vincenty inverse equations, accounting for Earth's flattening ($f = 1/298.257223563$). Accurate across intercontinental baselines (e.g., Cairo to Alexandria = $179.89\text{ km}$).
- **Polygon Area:** Calculates spheroidal/authalic polygon surface area across curved planetary geometry.
- **Elevation Invariance:** Visual terrain vertical exaggeration sliders (1x – 5x) alter only display relief and **never distort physical geodetic measurements**.

---

## 9. Basemaps, Terrain & Custom Imagery

### Built-in Basemaps (Ribbon Tab 5: Layers)
1. **Natural Earth Vector (Offline):** High-contrast vector basemap bundled locally for zero-network operation.
2. **OpenStreetMap Standard:** Global street, city, and transport network cartography.
3. **OpenTopoMap:** High-resolution topographic relief and contour styling.
4. **CartoDB Dark Matter:** Minimalist dark basemap ideal for vector feature overlay analysis.

### Custom Imagery Services
Click **Add Imagery Layer** in the Layers tab or sidebar to connect external map services:
- **XYZ Tile Servers:** Enter template URLs (e.g., `https://tile.example.com/{z}/{x}/{y}.png`).
- **OGC WMS / WMTS:** Connect to regional enterprise GIS servers.
- **Layer Controls:** Set layer opacity ($0\% - 100\%$), toggle visibility, reorder stacking order, or remove layers.

### Digital Elevation Models (DEM) & Exaggeration (Ribbon Tab 6)
- **Terrain Providers:** Automatic fallback to WGS84 Ellipsoid if online mesh endpoints fail.
- **Vertical Exaggeration:** Adjust relief scale using the slider from `0.2x` to `5.0x`, or choose quick presets (`1.0x`, `1.5x`, `2.0x`, `3.0x`, `5.0x`). Click **Reset (1.0x)** to return to natural terrain scale.

---

## 10. Vector Datasets (GeoJSON, Shapefile, GeoPackage)

Import external spatial datasets via Ribbon Tab 7 (**Tools -> Vector Data**):
- **GeoJSON (`.geojson`, `.json`):** Full support for FeatureCollections containing Points, LineStrings, and Polygons with coordinate reprojection.
- **ESRI Shapefile (`.shp`):** Binary reader for Point, PolyLine, and Polygon shape types. Companion `.prj` files are automatically parsed to detect coordinate reference systems.
- **Unknown Projection Safety (AC-008):** If a shapefile lacks a `.prj` file, Z EARTH prompts the user with the **CRS Selection Wizard** instead of making silent, potentially catastrophic projection assumptions.
- **OGC GeoPackage (`.gpkg`):** Ingests spatial feature tables into structured Temporary Places folders.

---

## 11. Bilingual UI & RTL Support

Z EARTH Desktop GIS provides native bilingual support for **English** and **Arabic**:
- Click the **Language Toggle** in Ribbon Tab 7 or press the language shortcut.
- The UI instantly switches between English and Arabic (`العربية`).
- **Dynamic RTL Layout:** When Arabic is active, the entire application layout flips to Right-to-Left (`FlowDirection.RightToLeft`), including ribbon groups, sidebar docking, dialog layouts, and status telemetry.

---

## 12. Keyboard Accelerators & Shortcuts Reference

| Shortcut | Command | Scope |
| :--- | :--- | :--- |
| `Ctrl+N` | New Project | Global |
| `Ctrl+O` | Open Project (`.zearth`) | Global |
| `Ctrl+S` | Save Project | Global |
| `Ctrl+Shift+S` | Save Project As... | Global |
| `Ctrl+Z` | Undo Last Action | Global |
| `Ctrl+Y` | Redo Last Action | Global |
| `Ctrl+B` | Toggle Sidebar Collapse / Expand | Global |
| `Ctrl+Shift+D` | Toggle Sidebar Left / Right Dock | Global |
| `Ctrl+T` | Toggle Dark / Light Theme | Global |
| `Home` | Fly to Home Preset | Viewport |
| `Ctrl+Shift+E` | Full Extent View | Viewport |
| `Alt+Left` | Previous Extent | Viewport |
| `Alt+Right` | Next Extent | Viewport |
| `N` | North Up (Reset Azimuth to $0^\circ$) | Viewport |
| `U` | Reset Pitch (Top-Down Nadir View) | Viewport |
| `+` / `Keypad +` | Zoom In | Viewport |
| `-` / `Keypad -` | Zoom Out | Viewport |
| `Delete` | Delete Selected Place Item | Places Tree |
| `Esc` | Cancel Current Drawing / Measurement | Viewport |
