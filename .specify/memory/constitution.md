<!--
SYNC IMPACT REPORT
==================
Version change: 0.0.0 (template) → 1.0.0
Modified principles:
  - [PRINCIPLE_1_NAME] → I. Spec-First Development (NON-NEGOTIABLE)
  - [PRINCIPLE_2_NAME] → II. Plan Before Code
  - [PRINCIPLE_3_NAME] → III. Test-First Discipline (NON-NEGOTIABLE)
  - [PRINCIPLE_4_NAME] → IV. Simplicity & YAGNI
  - [PRINCIPLE_5_NAME] → V. Incremental, Independently Testable Delivery
Added sections:
  - Quality & Workflow Constraints
  - Development Workflow
  - Governance
Removed sections: none (initial ratification)
Templates requiring updates:
  - ✅ .specify/templates/plan-template.md (Constitution Check gate already references this file — no edit needed)
  - ✅ .specify/templates/spec-template.md (Mandatory sections align with Principles I & V — no edit needed)
  - ✅ .specify/templates/tasks-template.md (Story-grouped, MVP-first ordering aligns with Principle V — no edit needed)
  - ✅ .claude/skills/speckit-*/SKILL.md (no agent-specific references requiring update)
  - ✅ CLAUDE.md (defers to current plan; no constitution-specific guidance to refresh)
Follow-up TODOs: none
-->

# ayato Constitution

## Core Principles

### I. Spec-First Development (NON-NEGOTIABLE)

Every feature MUST begin with a written specification produced via `/speckit-specify`
before any planning, task generation, or code is written. Specifications MUST capture
user stories with priorities, acceptance scenarios, functional requirements, and
measurable success criteria — and MUST remain technology-agnostic. Underspecified
areas MUST be resolved through `/speckit-clarify` or explicitly marked
`NEEDS CLARIFICATION` rather than guessed at during implementation.

**Rationale**: Spec-driven workflows collapse when implementation outruns intent;
locking the contract in writing early prevents drift, rework, and ambiguity in
downstream artifacts.

### II. Plan Before Code

An implementation plan (`plan.md`) MUST exist and pass the Constitution Check gate
before tasks are generated or code is written. Plans MUST document language,
dependencies, storage, testing strategy, project structure, and any constitutional
deviations in the Complexity Tracking section. Tasks (`tasks.md`) MUST be derived
from the plan and grouped by user story to preserve traceability.

**Rationale**: Plans are the bridge between intent and execution; skipping them
produces unverifiable work and hides architectural decisions from review.

### III. Test-First Discipline (NON-NEGOTIABLE)

When tests are included in the feature scope, they MUST be written before the
production code they cover, MUST be observed failing, and MUST then be made to pass
(Red → Green → Refactor). Contract tests MUST exist for every external interface
boundary identified in the plan. Integration tests MUST cover any change crossing
service or library boundaries, contract changes, and shared schemas.

**Rationale**: Tests written after code calcify the implementation rather than the
intent; enforcing test-first preserves the spec as the source of truth and makes
regressions visible at the moment of introduction.

### IV. Simplicity & YAGNI

The simplest design that satisfies the specification MUST be chosen. Abstractions,
configuration surfaces, fallbacks, and feature flags MUST NOT be added for
hypothetical future requirements. Any deviation from the simplest path MUST be
recorded in the plan's Complexity Tracking table with the rejected simpler
alternative and the concrete reason it was insufficient.

**Rationale**: Speculative complexity is the most common source of long-term
maintenance cost; requiring written justification makes the trade-off visible at
the moment it is incurred.

### V. Incremental, Independently Testable Delivery

Features MUST be decomposed into prioritized user stories (P1, P2, P3, …) where
each story is independently developable, testable, and deployable. The P1 story
MUST constitute a viable MVP on its own. Tasks MUST be organized so that
completing any single story yields working, demonstrable value without requiring
later stories.

**Rationale**: Independently shippable slices keep feedback loops short, reduce
integration risk, and ensure that an incomplete feature still ships value rather
than nothing.

## Quality & Workflow Constraints

- **Constitution Check Gate**: `/speckit-plan` MUST evaluate the plan against
  every principle above before Phase 0 research and again after Phase 1 design.
  Failures MUST be either resolved or recorded in Complexity Tracking with
  justification.
- **Cross-Artifact Consistency**: `/speckit-analyze` MUST be run after task
  generation when artifacts change materially; reported inconsistencies MUST be
  resolved before `/speckit-implement` proceeds.
- **Clarifications Are Authoritative**: Answers produced by `/speckit-clarify`
  MUST be encoded back into the spec; downstream artifacts MUST cite the spec,
  not chat history.
- **Boundary Validation Only**: Defensive validation, error handling, and
  fallbacks belong at system boundaries (user input, external APIs). Internal
  code MUST trust internal invariants rather than re-validating them.

## Development Workflow

The canonical pipeline is:

1. `/speckit-constitution` — establish or amend governing principles.
2. `/speckit-specify` — author the feature specification.
3. `/speckit-clarify` — resolve underspecified areas (optional but RECOMMENDED
   before planning).
4. `/speckit-plan` — produce `plan.md` and design artifacts; pass the
   Constitution Check.
5. `/speckit-tasks` — generate dependency-ordered, story-grouped tasks.
6. `/speckit-analyze` — verify cross-artifact consistency (RECOMMENDED).
7. `/speckit-implement` — execute tasks in order, honoring Test-First when tests
   are in scope.

Phases MUST NOT be skipped, but the optional steps (`clarify`, `analyze`) MAY be
omitted when the spec is unambiguous and artifacts are trivially consistent.
Extension hooks declared in `.specify/extensions.yml` (e.g., auto-commit hooks)
are part of the workflow and MUST be honored unless explicitly disabled.

## Governance

- This constitution supersedes ad-hoc practices, individual preferences, and any
  conflicting guidance in templates or agent instructions. Where conflict exists,
  the constitution wins and the conflicting artifact MUST be updated.
- **Amendments** MUST be made through `/speckit-constitution`, MUST include a
  Sync Impact Report at the top of this file, and MUST propagate updates to any
  dependent template (`plan-template.md`, `spec-template.md`, `tasks-template.md`)
  and runtime guidance (`CLAUDE.md`, `README.md` if present).
- **Versioning** follows semantic versioning:
  - **MAJOR**: Backward-incompatible removal or redefinition of a principle or
    governance rule.
  - **MINOR**: A new principle or section is added, or guidance is materially
    expanded.
  - **PATCH**: Wording, clarifications, typo fixes, non-semantic refinements.
- **Compliance Review**: Every PR MUST verify that changed artifacts comply with
  the principles above. Violations MUST be either fixed or recorded in the
  plan's Complexity Tracking with explicit justification before merge.
- **Runtime Guidance**: `CLAUDE.md` is the runtime guidance file for AI agents
  operating in this repository; it MUST point to the active plan rather than
  duplicating constitutional rules.

**Version**: 1.0.0 | **Ratified**: 2026-05-04 | **Last Amended**: 2026-05-04
