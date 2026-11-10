# Z EARTH — Google Earth functional-spec audit backlog

> Generated from the user-supplied Arabic behavior specification. The report status is
> documentary input only; `current_status` requires Z EARTH source/runtime evidence.

- Source SHA-256: `ac6350c18e340636cda6539842f15ee0bd23da459e2a79f6b09c746067516b34`
- Functional items: **151**
- Initial current status: DIFFERENT_BY_DESIGN=2, NEEDS_AUDIT=145, PARTIAL=3, PRESENT_UNVERIFIED=1

## Coverage by group

| Group | Count |
|---|---:|
| `ADD` | 11 |
| `ADV` | 10 |
| `DATA` | 10 |
| `EDIT` | 8 |
| `FILE` | 10 |
| `GRID` | 10 |
| `LAYER` | 12 |
| `OPT3D` | 9 |
| `OPTCACHE` | 3 |
| `OPTGEN` | 4 |
| `OPTNAV` | 9 |
| `OPTTOUR` | 3 |
| `TIME` | 8 |
| `TOOLS` | 14 |
| `UI` | 15 |
| `VIEW` | 15 |

## Feature ledger

| ID | Feature | Report status | Current status | Priority | Evidence |
|---|---|---|---|---|---|
| `ADV-001` | Sky mode | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P3` | `T0` |
| `ADV-002` | Stars / constellations | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P3` | `T0` |
| `ADV-003` | Moon / Mars modes | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P3` | `T0` |
| `ADV-004` | Directions From / To | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P3` | `T0` |
| `ADV-005` | Route Touring | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P3` | `T0` |
| `ADV-006` | Play Places Tour | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P3` | `T0` |
| `ADV-007` | Movie capture | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P3` | `T0` |
| `ADV-008` | Image export layout | `NEEDS_T3_AUDIT` | `PARTIAL` | `P1` | `T3` |
| `ADV-009` | Help / About | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P3` | `T0` |
| `ADV-010` | Update and Diagnostics | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P3` | `T0` |
| `UI-001` | نافذة العمل الرئيسية | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `UI-002` | Search panel | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `UI-003` | Places panel | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `UI-004` | Layers panel | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `UI-005` | 3D Viewer | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `UI-006` | Sidebar collapse | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `UI-007` | Overview map | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `UI-008` | Status bar | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `UI-009` | Navigation compass | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `UI-010` | Pan/zoom/tilt/rotate | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `UI-011` | Navigation HUD | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `UI-012` | Start location | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `UI-013` | Fullscreen | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `UI-014` | Scale legend | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `UI-015` | Tooltips/shortcuts | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `FILE-001` | Open | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `FILE-002` | Import | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `FILE-003` | Save Places | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `FILE-004` | Save Place As | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `FILE-005` | Save Image | `NEEDS_T3_AUDIT` | `PARTIAL` | `P1` | `T3` |
| `FILE-006` | Print | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `FILE-007` | Email | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `FILE-008` | View in Maps | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `FILE-009` | Recent Files | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `FILE-010` | Exit | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `EDIT-001` | Undo/Redo | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `EDIT-002` | Cut/Copy/Paste | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `EDIT-003` | Delete/Rename | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `EDIT-004` | Find in Places | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `EDIT-005` | Properties/Get Info | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `EDIT-006` | Show Elevation Profile | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `EDIT-007` | Selection/multi-select | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `EDIT-008` | Edit geometry | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `VIEW-001` | Show Toolbar | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `VIEW-002` | Show Sidebar | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `VIEW-003` | Status Bar | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `VIEW-004` | Overview Map | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `VIEW-005` | Compass/Navigation controls | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `VIEW-006` | Grid / Lat-Lon | `NEEDS_T3_AUDIT` | `PRESENT_UNVERIFIED` | `P0` | `T2` |
| `VIEW-007` | Atmosphere | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `VIEW-008` | Sunlight | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `VIEW-009` | Historical Imagery | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `VIEW-010` | 3D Buildings | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `VIEW-011` | Water/ocean | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `VIEW-012` | Reset Tilt/North | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `VIEW-013` | Make This My Start Location | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `VIEW-014` | View globe/2D | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `VIEW-015` | View terrain | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `ADD-001` | Placemark | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `ADD-002` | Folder | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `ADD-003` | Path | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `ADD-004` | Polygon | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `ADD-005` | Image Overlay | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `ADD-006` | Network Link | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `ADD-007` | 3D Model | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `ADD-008` | Tour | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `ADD-009` | Photo | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `ADD-010` | Circle | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `ADD-011` | Rectangle/Ellipse | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `TOOLS-001` | Ruler Line | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `TOOLS-002` | Ruler Path | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `TOOLS-003` | Ruler Polygon | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `TOOLS-004` | Ruler Circle | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `TOOLS-005` | Ruler 3D Path | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `TOOLS-006` | Ruler 3D Polygon | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `TOOLS-007` | Elevation Profile | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `TOOLS-008` | GPS Import | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `TOOLS-009` | Flight Simulator | `NEEDS_T3_AUDIT` | `PARTIAL` | `P3` | `T3` |
| `TOOLS-010` | Movie Maker | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `TOOLS-011` | Options / Preferences | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `TOOLS-012` | Cache / Repair | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `TOOLS-013` | Terrain controls | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `TOOLS-014` | Style Templates | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `OPT3D-001` | Coordinate display | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `OPT3D-002` | Elevation units | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `OPT3D-003` | Anisotropic filtering | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `OPT3D-004` | Graphics mode / safe mode | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `OPT3D-005` | Terrain quality | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `OPT3D-006` | Elevation exaggeration | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `OPT3D-007` | Icon/label size | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `OPT3D-008` | Show 3D imagery/buildings | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `OPT3D-009` | Display and antialiasing | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `OPTNAV-001` | Pan and Zoom | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `OPTNAV-002` | Flight Control/G-Force | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `OPTNAV-003` | Click and Zoom | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `OPTNAV-004` | Mouse Wheel Speed | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `OPTNAV-005` | Invert Wheel Zoom | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `OPTNAV-006` | Fly-to speed | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `OPTNAV-007` | Controller/joystick | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `OPTNAV-008` | Reverse controls | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `OPTNAV-009` | Reset and inertia | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `OPTGEN-001` | Language | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `OPTGEN-002` | Units of measurement | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `OPTGEN-003` | Show tips / startup | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `OPTGEN-004` | Telemetry and privacy | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `OPTCACHE-001` | Memory cache | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P2` | `T0` |
| `OPTCACHE-002` | Disk cache | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P2` | `T0` |
| `OPTCACHE-003` | Clear Cache | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P2` | `T0` |
| `OPTTOUR-001` | Tour speed | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P2` | `T0` |
| `OPTTOUR-002` | Pause between stops | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P2` | `T0` |
| `OPTTOUR-003` | Tour playback | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P2` | `T0` |
| `GRID-001` | Master Show Grid | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `GRID-002` | Show Longitude Lines | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `GRID-003` | Show Latitude Lines | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `GRID-004` | Intervals | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `GRID-005` | Major Geographic Lines | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `GRID-006` | Poles & origin anchors | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `GRID-007` | UTM Zones | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `GRID-008` | Grid label formats | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `GRID-009` | Depth/horizon clipping | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `GRID-010` | Persistence | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `TIME-001` | Sunlight UTC date | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P2` | `T0` |
| `TIME-002` | Sunlight hour slider | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P2` | `T0` |
| `TIME-003` | Now / realtime | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P2` | `T0` |
| `TIME-004` | Sunlight animation | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P2` | `T0` |
| `TIME-005` | Atmosphere independence | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P2` | `T0` |
| `TIME-006` | Historical date provenance | `NEEDS_T3_AUDIT` | `DIFFERENT_BY_DESIGN` | `P2` | `T2` |
| `TIME-007` | Time-aware KML | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P2` | `T0` |
| `TIME-008` | Availability per location | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P2` | `T0` |
| `LAYER-001` | Imagery basemaps | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `LAYER-002` | Borders & labels | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `LAYER-003` | Roads / transportation | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `LAYER-004` | Photos / POIs | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `LAYER-005` | 3D Buildings / models | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `LAYER-006` | Terrain DEM | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `LAYER-007` | Ocean bathymetry | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `LAYER-008` | Custom WMS/WMTS/TMS | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `LAYER-009` | NASA GIBS | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `LAYER-010` | Clouds | `NEEDS_T3_AUDIT` | `DIFFERENT_BY_DESIGN` | `P2` | `T2` |
| `LAYER-011` | Transparency/order | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `LAYER-012` | Attribution and licensing | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P1` | `T0` |
| `DATA-001` | KML 2.1/2.2 | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `DATA-002` | KMZ | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `DATA-003` | gx:Tour & gx:Track | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `DATA-004` | NetworkLink | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `DATA-005` | CSV import | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `DATA-006` | SHP/GPKG/GeoJSON | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `DATA-007` | GPS GPX | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `DATA-008` | Collada/3D model | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `DATA-009` | Atomic save/recovery | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |
| `DATA-010` | Local project persistence | `NEEDS_T3_AUDIT` | `NEEDS_AUDIT` | `P0` | `T0` |

## Status rule

A feature is not promoted to `PRESENT_VERIFIED` from a binding, mock, screenshot alone,
or the source report. Globe/UI claims require the relevant T3 Windows WebView2/GPU
workflow, while packaged release claims require T4 evidence.
