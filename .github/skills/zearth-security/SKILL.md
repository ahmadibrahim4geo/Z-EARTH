---
name: zearth-security
description: Use this skill for import security, network links, web bridge, credentials, installers and releases.
---
# Threat modeling and safety tests
- Enumerate untrusted data and trust boundaries before changing code.
- Block XXE/DTD, zip-slip/zip bombs, XSS in KML balloons and unexpected file URLs.
- Apply explicit network link consent, TTL/timeouts, host allowlists and rate limits.
- Store API keys outside git/project files; do not log tokens or private paths.
- Harden WebView2 navigation and validate trusted source for every message.
- Add malicious fixtures and regression tests before marking Done.

## Output
Report changes, exact tests/results, potential data loss, security, licensing, remaining limitations, and the next backlog task.
