---
name: zearth-desktop-ux
description: Use this skill when implementing the sidebar, context menus, native Windows widgets, themes or localization.
---
# WPF UX and layout consistency
- Build WPF MVVM components with keyboard commands, focus restoration and virtualized Places tree.
- English default UI; all user-facing strings via localization keys and complete Arabic RTL resources.
- One theme token source for native app and Cesium HTML overlays; check Light and Dark.
- Use familiar workflows but original visual identity; do not reproduce Google trade dress or icons.
- Verify right-click actions by node type and keyboard alternatives; update test matrix.
- Mock up high-density KML tree and small-screen/high-DPI cases.

## Output
Report changes, exact tests/results, potential data loss, security, licensing, remaining limitations, and the next backlog task.

## Explorer acceptance — no decorative controls
Verify `ClearSearchCommand` really empties search query/results/selection and prevents in-flight geocoder data from repopulating. Confirm Search/My Places `GridSplitter` and uncapped `TreeView` work in the real WPF window at multiple DPIs, sidebar docking/collapse, long Arabic names and both themes. Toolbar `WrapPanel` must keep Export Selected, Export All, Delete discoverable even when narrow. Run `ExplorerUiContractTests` but require native T3 screenshots and mouse-drag observations before claiming layout done. One-shot device location needs consent, correct WGS84 axis order and actual Windows permission testing. See [acceptance matrix](../../../docs/KML_EXPLORER_ACCEPTANCE_2026-10-25.md).
