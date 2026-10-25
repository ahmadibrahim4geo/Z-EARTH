---
name: zearth-snapping-measurement
description: Build one geometry-aware, indexed snapping service for both drawing and measuring in 2D/3D.
---

# zearth-snapping-measurement

1. Use shared world geometry + engine screen projection and depth pick.
2. Prioritize vertex/endpoint over edge on ties; support midpoint and configurable px tolerance.
3. Disable hidden layer snapping by default, render snap cues, allow modifier bypass.
4. Do not use vertical exaggeration in measurement geometry, provide named geodesic algorithms.
5. Run 10px/13px vs 12px tolerance tests in 2D/3D and varying DPI.

**Evidence gate:** report tests executed, exact failures and files changed. Do not claim complete from mock screenshots.
