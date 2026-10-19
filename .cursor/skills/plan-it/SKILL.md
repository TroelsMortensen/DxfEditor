---
name: plan-it
description: >-
  Write a human-reviewable plan into issues/ as NNN-slug.md with scope,
  architecture, mermaid diagrams, reasoning for choices, and a checklist.
  Use when planning a feature, saving work for later, or when the user asks
  to plan-it / write an issue plan.
---

# Plan It

Persist the plan in the repo under `issues/` so humans can review and park work for later. Prefer this over ephemeral chat-only or temp plan files.

If product or design choices are still ambiguous, use **ask-me** first (or alongside). Do not invent locked decisions.

## File location and naming

1. Ensure `issues/` exists at the repo root.
2. Pick the next sequential id: look at existing `issues/NNN-*.md` files; use `max(NNN) + 1`, zero-padded to 3 digits (`001`, `002`, …). If none exist, start at `001`.
3. Create `issues/NNN-short-slug.md` (slug: lowercase, hyphenated, e.g. `001-undo-redo.md`).
4. Write the plan **as you form it** (and update the same file when decisions change). Do not wait until “approve” to create the file.
5. Set status to something like `Planned (saved for later)` until implementation starts; then update status when building or done.

## What the plan must contain

Write for a **human reviewer**, not only for an agent. Include:

1. **Header** — Title with id (`# NNN — Feature name`), status, links to SRS / related issues.
2. **Scope** — In vs out (tables are fine). Locked product decisions called out clearly.
3. **Architecture** — Who owns what, main types/services, how UI wires in. Prefer a separate service over bloating large existing classes when that was agreed.
4. **Diagrams** — At least one Mermaid diagram when control flow or layering matters (flowchart and/or sequence). Keep node ids camelCase/PascalCase; no spaces in IDs; quote labels that need punctuation.
5. **Reasoning** — For non-obvious choices, state **why** (e.g. “commands not snapshots because parts hold dense polylines”; “EditorHistory separate so WorkspaceState stays document-only”). A short paragraph or bullets under the decision is enough.
6. **Integration map** — Concrete files/APIs to touch (paths relative to repo, markdown links when useful).
7. **Checklist** — Implementable checkboxes for the build phase.
8. **Out of scope** — Explicit later work so it does not leak into v1.

## Reasoning standard

Every meaningful fork that was decided should be readable without the chat:

- What was chosen
- What was rejected (briefly), when it helps
- Constraint or goal that drove the choice

Skip reasoning for trivial naming or purely mechanical steps.

## Diagram standard

- Use Mermaid in fenced `mermaid` blocks.
- Prefer simple flowcharts for ownership/wiring; sequence diagrams for request/gesture → state → history style flows.
- Follow Mermaid safety rules: no spaces in node IDs; quote tricky labels; avoid reserved words like bare `end` as IDs.

## Collaboration with ask-me

- Ambiguous → ask (ask-me), then fold answers into the issue file.
- After each batch of answers, update `issues/NNN-….md` so the file stays the source of truth.
- Chat can summarize; the issue file must stand alone for later sessions.

## Anti-patterns

- Leaving the only copy of the plan in chat or a Cursor temp plan file
- Plans that list steps with no diagrams or no “why”
- Guessing scope/architecture instead of asking
- Implementing in the same turn unless the user explicitly asked to build
