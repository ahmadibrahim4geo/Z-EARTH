---
name: gis-ui-command-audit
description: Inventory and audit GIS UI menus, ribbon buttons, toolbar icons, keyboard shortcuts and their real command handlers; diagnose dead controls and produce evidence-based command traceability reports without mistaking static bindings for working features.
license: MIT
compatibility: Static discovery is cross-platform; native runtime checks require the real target app and platform-specific automation tools.
---

# GIS UI Command Audit

**Purpose:** ensure every visible actionable control has a unique identity and a traceable, verified effect. Use for unresponsive buttons, mismatched icons, missing commands, Ribbon/toolbar/menu audits, duplicate handlers, dead shortcuts and incomplete command-to-engine bindings. This skill applies to *any* GIS program, not a particular product.

**Free-first source and testing policy:** follow the [repository free-first and licensing policy](../../references/free-first-policy.md). Reuse free/offline tools where feasible, disclose any paid GIS host/OS dependencies, and verify each upstream license before copying code. Never silently require a commercial service.

## Start here

1. Follow [Goal-first Intent Orchestrator](../prompt-intent-orchestrator/SKILL.md) and [GIS Router](../gis-development/SKILL.md); scope the user-approved application, repository, UI platforms, branches and expected results before writes. [Security Guardian](../gis-security-guardian/SKILL.md) is cross-cutting.
2. Read [shared GIS UI quality handoffs](../../references/gis-ui-quality-coordination.md) and the [command audit playbook](../../references/gis-ui-command-audit.md), then [upstream tools directory](../../references/gis-ui-quality-upstream-sources.md).
3. Inventory **all reachable surfaces**: main menu, Ribbon, context menus, floating and docked panels, property dialogs, status bar, keyboard shortcuts, GIS canvas tools, quick search, taskbar/system tray (where applicable). Count hidden/conditional commands separately.
4. Identify command ID, caption, icon asset, accessibility name, source file, UI event/binding, application controller, GIS operation, preconditions and side effects. Detect duplicate IDs, missing icon assets, orphaned shortcuts and commands with no declared binding.
5. Static discovery is not runtime proof. Use [offline read-only scanner](../../scripts/gis_ui_command_audit.py) for XAML/HTML declarations; report `DECLARED_BINDING`, `NO_BINDING` or `UNINSPECTED`. Inspect imperative/declarative event registrations, generated UI, and framework-specific conventions manually. **Do not report 'working' merely because a handler name exists.**
6. On a compatible host, route command cases to [Desktop E2E Testing](../gis-desktop-e2e-testing/SKILL.md) with a fixture and a visible output assertion. Only then mark `RUNTIME_VERIFIED`.
7. For import/save/export, coordinate with [GIS Data Flow Diagnostics](../gis-data-flow-diagnostics/SKILL.md); correctness requires output readback and GIS semantic fidelity, not a click or dialog success.
8. Document broken, partially working, disabled-by-design, inaccessible and **NOT RUN** states separately; propose minimal repairs, re-audit and log evidence in [Development Journal](../agent-development-journal/SKILL.md).

## Ownership and nonduplication

This skill **owns command discovery and binding traceability**, not the UX visual language (owned by [UI/UX Design & Testing](../gis-ui-ux-design-testing/SKILL.md)), actual platform interaction (Desktop E2E), geographic algorithms (Processing/CRS/KML), or user approval (Intent/host). A GIS ribbon button can exist but still fail engine and data-flow tests. One command ID must survive UI, application service, telemetry and E2E evidence; don't invent IDs from icon filenames.

## Deliverables and tests

- `command-inventory.json` and review report: counts by status, source evidence, handler/engine map, mismatched icon/label warnings, shortcuts and inaccessible controls.
- Unbound-control findings with precise file/line (when actually known); test plan pairing user action with verified GIS outcome.
- T0: inventory schema/links and static source checks; T1: synthetic fixtures; T2: actual GIS bridge; T3: real interactive UI; T4: packaged Windows/desktop build. Record PASS/FAIL/BLOCKED/NOT RUN, not guessed results.
- Try `python scripts/gis_ui_command_audit.py --root examples/ui-command-audit/fixture --format json` to see an **offline fixture**; it does not execute a GUI.

**Implementation boundary:** No arbitrary source execution, downloaded installers or credential scans. Ask host permission before any changes or GUI automation. Use paths relative to the inspected project.
