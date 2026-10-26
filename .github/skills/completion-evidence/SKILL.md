---
name: completion-evidence
description: Use whenever an AI agent is about to claim a task is complete, tested, fixed, sent, merged, published or delivered. DoneProof requires observed evidence for each acceptance criterion, with explicit verified, partial, failed, blocked and not-run outcomes rather than unsupported success claims.
license: MIT
---

# DoneProof — evidence before completion

**Cross-cutting:** use after the [Intent Orchestrator](../prompt-intent-orchestrator/SKILL.md) sets observable criteria and with the [GIS Router](../gis-development/SKILL.md), [Security Guardian](../gis-security-guardian/SKILL.md) and [Development Journal](../agent-development-journal/SKILL.md). Useful for non-GIS work too. Follow the [free-first policy](../../references/free-first-policy.md); do not assume paid services are available.

1. Recover **all required acceptance criteria** and the current revision. Do not drop inconvenient requirements.
2. Decide what would actually prove each claim: fresh command + exit status, saved artifact readback, provider receipt, or real host/UI operation.
3. Run allowed checks, inspect actual output and attach a reference, observation and current revision. Never fabricate logs, infer an external send/merge from drafts, or substitute T0/T1 static tests for native T2–T4 GIS/UI evidence. Third-party reports require independent corroboration.
4. Assign each criterion VERIFIED, PARTIAL, FAILED, BLOCKED or NOT RUN. No tool access or no permission is not success.
5. Claim overall completion **only if all required criteria are VERIFIED**. Report deliverables, remaining gaps, failed checks, evidence and exact limitations in the user's language. Re-verify after changes.

Optional structured report: [DoneProof JSON example](../../templates/doneproof.example.json) and [offline checker](../../scripts/verify_completion.py):

```bash
python scripts/verify_completion.py path/to/report.json
python scripts/verify_completion.py path/to/report.json --plan path/to/reviewed-plan.json --require-verified
```

Final release claims must use `--plan` to compare all required criteria with the original reviewed plan (matching request ID, task ID and revision). Include `request_id` in the report. Without a plan this is a structural diagnostic only, not release verification.

The checker validates **structure and claim consistency only**. It cannot run tests, authenticate a provider receipt or enforce agent tool dispatch. Follow the [full verification protocol](../../references/doneproof-verification-protocol.md) and [OpenAI Skill Creator / Superpowers evaluation](../../references/skill-creator-superpowers-review.md). Upstream tools are reference-only, not installed or copied.
