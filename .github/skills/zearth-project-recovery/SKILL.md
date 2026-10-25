---
name: zearth-project-recovery
description: Provide reliable .zearth project saves, temporary places, session recovery and portable asset handling.
---

# zearth-project-recovery

1. Use transactional atomic save, schema versioning, stable IDs, and export separate from Save Project.
2. Temporary Places survive crash via recoverable session journal; move into project only by user action.
3. Dirty prompts, autosave retention, no remote path side effects.
4. Guard project archive from unsafe zip paths and loss of original data.
5. Test Save/Open/Crash/portable project with moved path.

**Evidence gate:** report tests executed, exact failures and files changed. Do not claim complete from mock screenshots.
