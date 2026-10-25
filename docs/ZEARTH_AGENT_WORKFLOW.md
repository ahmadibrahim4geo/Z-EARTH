# Z EARTH — AI-agent execution and durable local development journal

**Source pin:** [GIS Development Skills PR #20](https://github.com/ahmadibrahim4geo/GIS-Development-Skills/pull/20), commit `ef26b76a134b89e950b616e30326df91bd226b43`. This upstream PR was **open when pinned**; refresh only in a separately verified review, never silently pull moving branch contents. The local [snapshot manifest](GIS_SKILLS_SNAPSHOT.json) records source content hashes.

## Why this integration exists

Z EARTH previously contained 19 generic GIS skills without a standardized intent-review and numbered progress recording workflow. This update brings two cross-cutting skills and preserves all Z EARTH/CesiumJS specialists:

`User request → Prompt Intent Orchestrator → user/host review → GIS router + relevant Z EARTH specialties + Security Guardian → real tool execution → test and source readback → Agent Development Journal → handoff`.

These are **instructions and offline Python utilities for coding agents**, not an LLM, event observer, built-in Z EARTH application feature or automatic source-code patch service. They do not execute at application startup.

## Operator use from the Z EARTH working tree

Use Python 3 standard library. Start from the actual project root:

```bash
python scripts/validate-agent-skills.py
python .github/scripts/intent_router.py --text "Z EARTH: fix terrain exaggeration and UTM grid" --format json
python .github/scripts/intent_router.py --plan .github/examples/intent-orchestrator/example-plan.json
python -m unittest discover -s .github/tests -p "test_intent_orchestrator.py" -v
python -m unittest discover -s .github/tests -p "test_development_journal.py" -v
```

The sample plan is a **draft**. Running the same plan with `--gate` is expected to reject its missing approval; even an accepted boolean declaration would **not** authenticate a real user or authorize execution.

After explicit authorization to create local project state:

```bash
python .github/scripts/development_journal.py init --project .
python .github/scripts/development_journal.py add --project . --kind request --status proposed --summary "Improve globe coordinates" --details "Preserve existing project files; review requested scope" --task-id ZEARTH-GRID-1 --plan-revision r1
python .github/scripts/development_journal.py verify --project .
python .github/scripts/development_journal.py status --project .
```

After any actual work, record a new event with truthful affected files, Git commit, specific test commands/results and links to earlier reports. A proposed revision, cancellation or rollback never erases earlier canonical records. `repair` regenerates only derived reports from checksum-verified records. Hashes detect accidental corruption, not malicious editing or authenticated approval.

### Protected workspace and permissions

- The log lives in `.agent-development-log/` inside the **actual project checkout** and is listed in `.gitignore`. It is private and not automatically uploaded. Never put API keys, passwords, sensitive machine paths, exact location, full private prompts or untrusted instructions in records.
- The journal helper writes local history only; it does not modify source code, commit Git files, run rollback or watch WPF application commands. A hosting agent calls it at safe checkpoints.
- Require actual permission for consequential writes, data deletion, external transmission and paid or licensed sources. Structural plan validation is not an authorization mechanism.
- For Z EARTH, verify source paths `MainWindow.xaml → MainViewModel → GlobeBridge → cesium-engine.js`, settings persistence, KML/KMZ preservation and security before implementation. Match observed native behaviors to acceptance contracts; never claim T3/T4 from tests on mocked/CI engines.

### Update / rollback model

An agent keeps one per-project chronological log locally and uses Git commits/PRs as the independent source-of-code truth. A `rollback` journal event records a proposed or *verified* rollback, but cannot itself undo files. Agents must inspect and obtain permission before any real revert.

See [existing acceptance procedure](FRESH_CLONE_VALIDATION.md), [TERRAIN_GRID_ACCEPTANCE](TERRAIN_GRID_ACCEPTANCE.md), and [project adapter](../.github/skills/zearth-agent-workflow/SKILL.md).
