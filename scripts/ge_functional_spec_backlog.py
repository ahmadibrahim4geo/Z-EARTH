#!/usr/bin/env python3
"""Build a deterministic Z EARTH audit backlog from the Arabic GE feature spec.

The source document is a behavior/reference catalog.  Its status column is kept
as report evidence only; it never becomes the current application status unless
an explicit, reviewed override supplies source or runtime evidence.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from collections import Counter
from pathlib import Path
from typing import Any


FEATURE_ID = re.compile(r"^[A-Z][A-Z0-9]*-\d{3}$")

DEFAULT_PRIORITY = {
    "ADV": "P3",
    "UI": "P0",
    "FILE": "P0",
    "EDIT": "P0",
    "VIEW": "P1",
    "ADD": "P1",
    "TOOLS": "P1",
    "OPT3D": "P1",
    "OPTNAV": "P1",
    "OPTGEN": "P1",
    "OPTCACHE": "P2",
    "OPTTOUR": "P2",
    "GRID": "P0",
    "TIME": "P2",
    "LAYER": "P1",
    "DATA": "P0",
}

ALLOWED_CURRENT_STATUS = {
    "NEEDS_AUDIT",
    "PRESENT_VERIFIED",
    "PRESENT_UNVERIFIED",
    "PRESENT_BROKEN",
    "MISSING_CONFIRMED",
    "PARTIAL",
    "DIFFERENT_BY_DESIGN",
    "PROVIDER_RESTRICTED",
    "NOT_APPLICABLE",
}


def parse_feature_rows(text: str) -> list[dict[str, Any]]:
    items: list[dict[str, Any]] = []
    seen: set[str] = set()
    for line_number, line in enumerate(text.splitlines(), start=1):
        if not line.startswith("|"):
            continue
        cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
        if len(cells) != 5:
            continue
        feature_id = cells[0].strip("`")
        if not FEATURE_ID.fullmatch(feature_id):
            continue
        if feature_id in seen:
            raise ValueError(f"duplicate feature ID {feature_id} at line {line_number}")
        seen.add(feature_id)
        group = feature_id.split("-", 1)[0]
        items.append(
            {
                "id": feature_id,
                "group": group,
                "feature": cells[1],
                "reference_behavior": cells[2],
                "acceptance_requirement": cells[3],
                "report_status": cells[4].strip("`"),
                "current_status": "NEEDS_AUDIT",
                "priority": DEFAULT_PRIORITY.get(group, "P2"),
                "evidence_tier": "T0",
                "source_evidence": [],
                "runtime_evidence": [],
                "notes": "",
                "source_line": line_number,
            }
        )
    return items


def apply_overrides(items: list[dict[str, Any]], overrides: dict[str, Any]) -> None:
    by_id = {item["id"]: item for item in items}
    unknown = sorted(set(overrides) - set(by_id))
    if unknown:
        raise ValueError(f"override IDs not found in source specification: {', '.join(unknown)}")
    for feature_id, values in overrides.items():
        if not isinstance(values, dict):
            raise ValueError(f"override for {feature_id} must be an object")
        current_status = values.get("current_status")
        if current_status is not None and current_status not in ALLOWED_CURRENT_STATUS:
            raise ValueError(f"unsupported current_status for {feature_id}: {current_status}")
        by_id[feature_id].update(values)


def render_markdown(payload: dict[str, Any]) -> str:
    items = payload["features"]
    group_counts = Counter(item["group"] for item in items)
    status_counts = Counter(item["current_status"] for item in items)
    lines = [
        "# Z EARTH — Google Earth functional-spec audit backlog",
        "",
        "> Generated from the user-supplied Arabic behavior specification. The report status is",
        "> documentary input only; `current_status` requires Z EARTH source/runtime evidence.",
        "",
        f"- Source SHA-256: `{payload['source_sha256']}`",
        f"- Functional items: **{payload['feature_count']}**",
        f"- Initial current status: " + ", ".join(f"{k}={v}" for k, v in sorted(status_counts.items())),
        "",
        "## Coverage by group",
        "",
        "| Group | Count |",
        "|---|---:|",
    ]
    lines.extend(f"| `{group}` | {count} |" for group, count in sorted(group_counts.items()))
    lines.extend(
        [
            "",
            "## Feature ledger",
            "",
            "| ID | Feature | Report status | Current status | Priority | Evidence |",
            "|---|---|---|---|---|---|",
        ]
    )
    for item in items:
        feature = item["feature"].replace("|", "\\|")
        lines.append(
            f"| `{item['id']}` | {feature} | `{item['report_status']}` | "
            f"`{item['current_status']}` | `{item['priority']}` | `{item['evidence_tier']}` |"
        )
    lines.extend(
        [
            "",
            "## Status rule",
            "",
            "A feature is not promoted to `PRESENT_VERIFIED` from a binding, mock, screenshot alone,",
            "or the source report. Globe/UI claims require the relevant T3 Windows WebView2/GPU",
            "workflow, while packaged release claims require T4 evidence.",
            "",
        ]
    )
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--overrides", type=Path)
    parser.add_argument("--json-output", required=True, type=Path)
    parser.add_argument("--markdown-output", required=True, type=Path)
    args = parser.parse_args()

    source_bytes = args.input.read_bytes()
    source_text = source_bytes.decode("utf-8-sig")
    features = parse_feature_rows(source_text)
    if len(features) != 151:
        raise ValueError(f"expected 151 feature rows, found {len(features)}")

    if args.overrides:
        override_payload = json.loads(args.overrides.read_text(encoding="utf-8-sig"))
        if not isinstance(override_payload, dict):
            raise ValueError("override file must contain an object keyed by feature ID")
        apply_overrides(features, override_payload)

    payload = {
        "schema_version": 1,
        "source_name": args.input.name,
        "source_sha256": hashlib.sha256(source_bytes).hexdigest(),
        "feature_count": len(features),
        "status_policy": "report_status_is_documentary; current_status_requires_Z_EARTH_evidence",
        "features": features,
    }

    args.json_output.parent.mkdir(parents=True, exist_ok=True)
    args.markdown_output.parent.mkdir(parents=True, exist_ok=True)
    args.json_output.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n"
    )
    args.markdown_output.write_text(render_markdown(payload), encoding="utf-8", newline="\n")
    print(f"Wrote {len(features)} features from {args.input.name}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
