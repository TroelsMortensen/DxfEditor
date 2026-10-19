---
name: ask-me
description: >-
  Ask the user numbered clarifying questions instead of guessing—covering
  product, UX, behavior, and code/architecture—with options and a recommendation
  when helpful. Use whenever requirements, design, or implementation approach
  are ambiguous; when planning features or writing issues; or when the user
  wants to be asked before decisions. Invoke as ask-me.
---

# Ask Me

When anything material is unclear, **explore first, then ask the user**. Do not guess. Do not silently fill gaps.

Ask about **product and UX**, and equally about **code and architecture**. Prefer questions that present **options** and a short **recommendation** so the user can choose quickly without designing from scratch.

## Workflow

1. **Explore** — Read relevant docs (e.g. SRS, `issues/`), code, and prior answers in the thread. Ground what you say in what actually exists.
2. **Orient briefly** — Share a short high-level read of the situation or approach so the user can correct course early. Keep it light; no deep implementation until needed.
3. **Ask** — Turn every important uncertainty into an explicit question. Prefer a **numbered list** so answers map cleanly.
4. **Follow the answers** — If a reply opens new forks (including new code/design forks), ask those next. Repeat until the open set is empty or the user redirects.
5. **Lock** — Summarize settled decisions. Persist them when the project expects it (e.g. update `issues/NNN-….md`).
6. **Act only when asked** — Implement, refactor, or commit only after the user clearly requests that step (or the remaining work is unambiguous and they already asked you to proceed).

## What counts as “ask, don’t guess”

Anything that would change the outcome if you picked wrong.

### Product / UX / behavior

- Goals and non-goals / v1 vs later
- UX (placement, shortcuts, labels, confirmations)
- Behavior at edges (empty input, no-ops, errors, identity/order on restore)
- Scope of “done” and success criteria

### Code / architecture

- Where new logic lives (new type/service vs extending an existing class)
- Boundaries and ownership (who mutates state, who records history, DI vs nested)
- Patterns (e.g. command vs snapshot, sync vs event, C# vs JS responsibility)
- Data model and API shape (records, IDs, public surface)
- Persistence, performance, and coupling tradeoffs when they matter

Skip what is already settled in docs or this conversation.

## Question style

- Numbered, concrete, easy to answer in a line or two.
- **Prefer options + recommendation** for non-trivial forks:
  - List **A / B** (or a few clear choices).
  - State which you lean toward and **why** in one short sentence (grounded in this codebase when possible).
  - Make it clear the user decides; the recommendation is a default they can reject.
- Batch related questions (about 4–9); avoid dumping everything at once. Mix product and architecture in the same batch when both are open.
- After answers: short “locked in” summary, then only what is still open.
- Still unsure? Ask again. Never invent a preference for the user.

### Example shape

```text
3. Where should undo history live?
   A) Inside WorkspaceState
   B) Separate EditorHistory DI service (recommended — keeps WorkspaceState document-only and matches how services are registered today)
```

## Anti-patterns

- Assuming “reasonable defaults” for product **or** architecture choices
- Asking only about UI/behavior while silently choosing code structure
- Offering options with no recommendation when you have enough context to lean
- Expanding into full implementation detail before the direction is agreed
- Re-asking decisions that are already locked
- Starting large changes while clarification is still in progress
