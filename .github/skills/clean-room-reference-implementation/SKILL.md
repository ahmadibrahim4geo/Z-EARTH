---
name: clean-room-reference-implementation
description: Analyze permitted reference behavior and independently build/test interoperable GIS functionality using provenance and evidence gates.
license: MIT
---

# Clean-Room Reference Implementation — GIS Development Skills


**Free-first licensing:** [policy and commercial exceptions](../../references/free-first-policy.md).

## Purpose
Reproduce observable functionality and interoperable data behavior of reference software using independently authored code, without copying protected source, assets, credentials, or restricted services.

## Roles and separation
1. **Reference observer**: exercise permitted public UI, record inputs/outputs, versions, screenshots where authorized, and reproducible steps. Do not inspect disassembly, proprietary source, or bypass protections.
2. **Specification reviewer**: convert observations and public standards into implementation-neutral requirements, with provenance and legal review flags.
3. **Independent implementer**: receive only approved specifications and public standards; implement with original code and properly licensed dependencies.
4. **Verification agent**: run the same test cases against both products where permitted, record deviations, regressions, and reproducible evidence.
5. **Security/licensing gate**: review permissions, asset licensing, privacy, API terms, and third-party notices before release.

Strict clean-room claims require enforceable team/context separation and documented information barriers. AI agents sharing the same context are **not** a strict clean room; describe that as reference-driven independent implementation instead.

## Workflow
INTENT -> reference inventory -> allowed observation -> behavior specification -> provenance/license gate -> independent design -> implementation -> unit/integration/UI tests -> reference parity matrix -> evidence-based acceptance -> release.

Each requirement: ID, reference product/version, feature, setup, steps, expected observable behavior, edge cases, input fixtures, output format, public documentation, restrictions, implementer handoff, tests, observed result, confidence, status.

Never mark a feature DONE based solely on UI presence, mock/fake engine, code review, or green CI. For desktop globe commands require native Windows + WebView2/GPU execution evidence; otherwise mark NOT VERIFIED.

## First ZEarth pilot
Use Google Earth Pro as a functional reference, not a source of code or imagery. Start with Placemark create/edit/delete, folder persistence, KML/KMZ round-trip, camera navigation, and grid toggles. Follow with sunlight, search, layers, and interoperability. Respect Google trademarks and data/service licensing.

Example acceptance for placemark: create WGS84 point with name, description and icon; save KML; close and reopen project; import saved KML; verify coordinates, styling where supported, tree state and visibility. Record unsupported features explicitly.

## Integration
Route through existing GIS intent router, security and MCP capability authorization, KML/KMZ, UI command audit, desktop end-to-end tests and GIS data-flow diagnostics. MCP capability discovery is not permission; require explicit authorization for side effects. Do not silently replace existing skills.

## ZEarth twelve-role reference lab example

The [ZEarth reference lab](https://github.com/ahmadibrahim4geo/Z-EARTH/tree/main/docs/google-earth-reference) contains eight **reference-analysis workstreams** R01–R08 and four **implementation audits** Z01–Z04. The manifest, distinct reports and 30-feature comparison are auditable. The `scripts/reference_lab_audit.py` runner checks each role's source-evidence contract; GitHub Actions runs twelve read-only role jobs and retains their reports.

**Evidence boundary:** these are deterministic auditors and role-specific authored analyses, **not independent LLM agents** or a real Windows comparison. An external agent host may dispatch the roles individually using the manifest, subject to scoped tool access, provenance, review and licensing.

Example:
```bash
python scripts/reference_lab_audit.py --all --out-dir reference-evidence
node --test tests/cloud-layer.test.cjs
```

## Release gates
- Provenance and licensing checked
- Original code and approved dependencies
- Reproducible fixtures and negative cases
- Native UI evidence where required
- No regression in KML round-trip or project save/open
- README/index and changelog describe only verified work
