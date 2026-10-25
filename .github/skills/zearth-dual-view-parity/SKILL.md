---
name: zearth-dual-view-parity
description: Keep all core spatial features synchronized across Cesium 3D and 2D (optional later MapLibre).
---

# zearth-dual-view-parity

1. Use renderer-neutral feature model and use cases.
2. Implement actual mode switch with 3D and 2D camera states; do not duplicate layers.
3. Keep drawing/snapping/measure/selection/Save/Undo core commands active in both views.
4. Document all engine-specific capability differences and do not falsely advertise unsupported functions.
5. Run 100 toggles plus edit consistency regression tests.

**Evidence gate:** report tests executed, exact failures and files changed. Do not claim complete from mock screenshots.
