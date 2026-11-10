---
name: gis-ui-ux-design-testing
description: Design, review and test accessible GIS application interfaces and user journeys for desktop, web and mobile, including bilingual RTL/LTR, layers/map interaction, responsive panels, design systems, visual regression and usability acceptance.
license: MIT
compatibility: Design reviews and static checks are cross-platform; visual and native usability assertions require supported real browsers, displays, users and/or GIS host apps.
---

# GIS UI/UX Design & Testing

**Purpose:** create professional, consistent, discoverable GIS interfaces with **working behavior and verifiable usability**, rather than decorative mockups or inert buttons. Applies to standalone GIS, ArcGIS/QGIS plugins, map websites and field/mobile clients.

**Free-first source and testing policy:** follow the [repository free-first and licensing policy](../../references/free-first-policy.md). Reuse free/offline tools where feasible, disclose any paid GIS host/OS dependencies, and verify each upstream license before copying code. Never silently require a commercial service.

## Design-to-verification route

1. Receive the reviewed user goal and target audience from [Intent Orchestrator](../prompt-intent-orchestrator/SKILL.md) through the [GIS Router](../gis-development/SKILL.md); consult [Security Guardian](../gis-security-guardian/SKILL.md). Preserve the user's approved theme, identity, branding, language, platform and protected footer/author content.
2. Read [shared GIS quality handoffs](../../references/gis-ui-quality-coordination.md), [UI/UX design and testing playbook](../../references/gis-ui-ux-design-testing.md) and [verified public upstream examples](../../references/gis-ui-quality-upstream-sources.md).
3. Inventory GIS personas and workflows: novice/expert, search → layer discovery → open/edit → map navigation → analysis → export/save/reopen, error recovery, disabled/offline states, shortcut and keyboard use, 2D/3D transitions and panel resize.
4. Create information architecture first: navigation, Menu/Ribbon, Layers and Places, searchable panels, dialogs, inspectors, status bar, coordinate display, accessible icon labels, notification/toast behavior. Distinguish GIS layer symbology (owned by [Cartography](../gis-cartography-symbology/SKILL.md)) from shell/control UI components (owned here).
5. Specify platform-appropriate design tokens and component states: typography, spacing, scale, color/contrast, hover/focus/disabled/loading/error/success, density, RTL/LTR and high-DPI. Avoid hardcoded per-control cosmetics and fragile icon text.
6. Prototype and implement real interactions with logical task completion; verify that commands hand to [Command Audit](../gis-ui-command-audit/SKILL.md), real GIS operations through [Data Flow Diagnostics](../gis-data-flow-diagnostics/SKILL.md) and native journeys through [Desktop E2E](../gis-desktop-e2e-testing/SKILL.md).
7. Test keyboard-only paths, screen readers, focus order, resize/minimum width, touch targets where applicable, language switch, Arabic BiDi/Unicode, English-only UI, localization expansion, high contrast, color vision deficiencies, error and empty states, offline UI, long-running operations and visual regressions across declared target platforms.
8. Record before/after screenshots only with authorized nonprivate fixtures, the expected user journey, actual usability observations, automated accessibility findings and manual checks. A passing axe rule does not certify a usable GIS workflow.

## Shared skill boundaries

- UX controls the *application shell, interaction and design system*; [Cartography](../gis-cartography-symbology/SKILL.md) controls scientific map symbol semantics.
- [Internationalization](../gis-internationalization-arabic/SKILL.md) controls translation, shaping, BiDi, search and localized data storage; UX validates usable layout at the interface boundary.
- This skill documents what a user should experience; **Command Audit** traces binding; **E2E** proves the real user journey; **Data Flow** proves the GIS payload; **Security** enforces safe telemetry and operations.
- Prefer host-native Qt/WPF/WinUI component systems or accessible web controls as appropriate. Do not require a Windows-only or commercial design toolkit for all users.

**Deliverables:** UI map/wireflow, design tokens/component contract, keyboard/accessibility matrix, RTL/LTR and DPI screenshots, semantic test scenarios, defect prioritization and verified vs NOT RUN test tier outcomes. Use the [UI/UX checklist](../../examples/ui-ux/README.md) for acceptance examples.

## OS/device consent and failure UX

For [System Capabilities & Permissions](../system-capabilities-and-permissions/SKILL.md), design accessible, bilingual permission rationale, allow/deny/revoke/timeout/missing-hardware states, user-invoked retry and manual/offline alternatives. Never fake a native OS permission prompt with a site dialog. Verify keyboard and RTL behavior; pass native proof to Desktop E2E.

## Visual assets (conditional)

When the reviewed project needs thumbnails, official logos, photographs, illustrations or screenshots, compose [Visual Asset Discovery & Integration](../visual-asset-discovery-integration/SKILL.md). It owns internet/source discovery, asset suitability, source credits and prepared local variants; keep layout, target resource binding, packaging and actual UI acceptance with this platform/UX owner. Do not silently replace requested images with generic icons or claim a source URL proves local integration.
