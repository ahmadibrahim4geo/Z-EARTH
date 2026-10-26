# Google Earth Reference Framework — controlled research

## Scope and evidence (2026-10-26)
User-authorized Google Drive folder: https://drive.google.com/drive/folders/1_G-2MOuE2qd53WMrC7zYhY_sVgi-Rrs0?usp=drive_link . Read-only directory inventory was performed through the connected Drive account. **No Google binaries executed, decompiled, or copied into this repository.** Filenames imply component categories but do not prove runtime behavior.

Observed `client/`: Qt5Widgets.dll, Qt5Gui.dll, Qt5OpenGL.dll, Qt5Network.dll, Qt5WebKit.dll, Qt5Positioning.dll, qt.conf, drivers.ini; subfolders `shaders/`, `plugins/`, `res/`, `lang/`, `alchemy/`.
Observed `client/shaders/`: atmosphere.glslesf/v, lighting.h, stars.glslesf/v, watersurface.glslesf/v, hammer_aitoff.glslesf/v, viewshed.h and vegetation shader variants. These are names only, not validated feature contracts.
Observed `client/res/`: navcontrols.rcc, toolbar.rcc, leftpanel-layer.rcc, search.rcc, tourcontrols.rcc, tmcontrols.rcc, terrainmgr.rcc, default_myplaces.rcc, userpalette.kml, and more.
Observed `client/plugins/`: imageformats, platforms, printsupport, audio, mediaservice, bearer and npgeinprocessplugin.dll.

## 8 reference research workstreams (proposed, not independently executed agents)
R1 installation inventory and provenance; R2 UI commands and menus; R3 globe/navigation and camera; R4 shaders, atmosphere, lighting; R5 KML/KMZ and places; R6 layers/grid/coordinates; R7 search, gazetteer, network; R8 stability, settings, resources and licensing.

## 4 ZEarth audit workstreams (proposed)
Z1 WPF ribbon/UI command dispatch; Z2 Cesium/WebView2 GlobeBridge and camera; Z3 KML/KMZ, Places, project persistence; Z4 grid/sunlight/search, integration testing and performance.

## Implementation rules
Use [clean-room skill](https://github.com/ahmadibrahim4geo/GIS-Development-Skills/blob/main/skills/clean-room-reference-implementation/SKILL.md), existing [parity contracts](../GOOGLE_EARTH_PRO_PARITY.md) and [pilot](../CLEAN_ROOM_PARITY_PILOT.md). Keep ZEarth-specific enhancements (2D/3D modes, north controls, grid options); never replace them merely to resemble the reference. Distinguish reference observations from assumptions. Each workstream must produce findings, evidence source, ZEarth code paths, reproducible test, risk, priority, owner, and verification status.

## Acceptance
Native Windows/WebView2 tests required for claims about runtime parity, responsiveness and graphical fidelity. FakeGlobeEngine or source review alone is not enough. Do not redistribute Google binaries, shaders, RCC assets, imagery or service credentials.
