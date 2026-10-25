---
name: zearth-camera-navigation
description: Make Earth globe impossible to lose irrecoverably under rapid mouse/touch navigation.
---

# zearth-camera-navigation

1. Implement central camera state and last-known-good recovery.
2. Provide Home, North Up, Full/Selection extent, +/- fixed zoom with config, Previous/Next extent.
3. Clamp zoom, roll/tilt, recover invalid state, handle horizon, low FPS and resize.
4. Do not confuse edit Undo/Redo and camera history.
5. Run camera stress matrix from docs/05_CAMERA_NAVIGATION_AR.md.

**Evidence gate:** report tests executed, exact failures and files changed. Do not claim complete from mock screenshots.
