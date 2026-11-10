# Phase 00 — Official source preflight

## Source identity

- Official remote: `https://github.com/ahmadibrahim4geo/Z-EARTH.git`
- Remote default branch: `main`
- Verified remote/main commit: `6696a8ed179206d7769eeddc5e9b51e3a1fa30a6`
- Local base commit: identical to remote/main at preflight time
- Working branch: `repair/full-verification-2026-10-27`
- The earlier workspace root was a ZIP copy without `.git`; implementation moved to the clean tracked checkout `canonical-main`.

The requested `ZEarth_Agent_Implementation_Directive_2026-10-27.md` was not present in the official checkout or supplied attachments. The user's current implementation message is treated as the approved directive. The audit report remains available in the parent workspace at `../Development Reports/ZEarth_Full_System_Audit_2026-10-26.md`.

## Baseline results

| Gate | Result |
|---|---|
| `dotnet restore ZEarth.sln` | PASS |
| `dotnet build ZEarth.sln -c Release --no-restore` | PASS, 0 warnings, 0 errors |
| Full MSTest | FAIL: 240 passed, 3 failed, 243 total |
| Failing tests | Wayback date parsing; two CRS/telemetry tests |
| Each CRS test in a fresh process | PASS, proving cross-test/user-settings contamination |
| Wayback test in a fresh process | FAIL, proving deterministic timezone defect |
| Skills snapshot validation | FAIL on all pinned files in a fresh Windows clone |

## Root causes established during preflight

1. `WaybackService.TryParseDate` interprets date-only values as UTC and converts them to Cairo local time.
2. `SettingsService` reads and writes the real user's `%LocalAppData%/ZEarth/settings.json` during tests. The two CRS tests depend on ambient `MapCrsEpsg`; the full suite later writes `4326` to the user's settings. Test state is therefore not isolated.
3. The skill manifest stores canonical Git blob hashes, while `validate-agent-skills.py` hashes raw checkout bytes. Global Git `core.autocrlf=true` converts LF to CRLF, causing every pinned text file to fail validation in a normal Windows clone.
4. The T3 CRS contradiction from ZE-AUD-001 remains in current source: `StatusCrs` follows coordinate display format while center/cursor values follow `MapCrsEpsg`.

## Next phase

Repair the three test failures without skips, isolate settings tests, make skill integrity checking line-ending-safe, and correct the CRS status single source of truth. Then rerun the complete build, MSTest, Node, skills, security, and helper suites before broader command work.

