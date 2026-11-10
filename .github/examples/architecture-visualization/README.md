# Shared visualization example

This example explains the **declared** GIS skill routing and optional Archify handoff. It is independently authored from this pack's documented responsibilities. It is not a live agent trace, native GIS test or measured runtime architecture.

- [Specialist](../../skills/gis-architecture-visualization/SKILL.md)
- [Invocation, dependency and delivery guide](../../references/archify-integration.md)
- [Draft plan](plan.example.json)
- [Editable Archify workflow](skill-routing.workflow.json)
- Generated HTML and upstream verification receipts are intentionally not vendored in Z EARTH; regenerate them with the reviewed Archify runtime when this optional example is exercised.

Run the offline planning preview from the repository root:

```bash
python scripts/intent_router.py --plan examples/architecture-visualization/plan.example.json --workflow --format json
```

The plan is intentionally unapproved. `--gate` must block it. The preview selects a diagram owner and carries explicit purpose/type/evidence/language; it never starts a tool or grants access.

To render the workflow, the host discovers the reviewed external Archify runtime and Node.js, then invokes `finalize` as described in the integration guide. Keep its real output/check receipts together. Viewer inspection is separate from artifact validation and from actual GIS execution. Upstream's Apache-2.0/component notices must remain embedded in generated HTML.

No spatial data is read or written. CRS, coordinates, units and source datasets are not applicable to this abstract roles/command workflow. Preserve original files; use new output locations and host-authorized installation/network scope.

Evidence levels: the plan/router regression suite is offline T0/T1; an actual Archify render/browser run is diagram evidence only. Native GIS T2–T4 tests are **NOT RUN / not applicable** to this documentation example. No Archify render or browser acceptance is claimed by this Z EARTH snapshot.

