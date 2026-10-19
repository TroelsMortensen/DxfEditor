---
name: ask-me
description: >-
  Ask the user numbered clarifying questions instead of guessing. Use whenever
  requirements, design, behavior, or architecture are ambiguous; when planning
  features or writing issues; or when the user wants to be asked before
  decisions or implementation. Invoke as ask-me.
---

# Ask Me

When anything material is unclear, **explore first, then ask the user**. Do not guess. Do not silently fill gaps.

## Workflow

1. **Explore** — Read relevant docs (e.g. SRS, `issues/`), code, and prior answers in the thread. Ground what you say in what actually exists.
2. **Orient briefly** — Share a short high-level read of the situation or approach so the user can correct course early. Keep it light; no deep implementation until needed.
3. **Ask** — Turn every important uncertainty into an explicit question. Prefer a **numbered list** so answers map cleanly.
4. **Follow the answers** — If a reply opens new forks, ask those next. Repeat until the open set is empty or the user redirects.
5. **Lock** — Summarize settled decisions. Persist them when the project expects it (e.g. update `issues/NNN-….md`).
6. **Act only when asked** — Implement, refactor, or commit only after the user clearly requests that step (or the remaining work is unambiguous and they already asked you to proceed).

## What counts as “ask, don’t guess”

Anything that would change the outcome if you picked wrong, including:

- Goals and non-goals / v1 vs later
- UX (placement, shortcuts, labels, confirmations)
- Behavior at edges (empty input, no-ops, errors, identity/order on restore)
- Architecture boundaries (new type/service vs extending an existing one)
- Data model and persistence choices
- Scope of “done” and success criteria

Skip what is already settled in docs or this conversation.

## Question style

- Numbered, concrete, easy to answer in a line or two (offer A/B when helpful).
- Batch related questions (about 4–9); avoid dumping everything at once.
- After answers: short “locked in” summary, then only what is still open.
- Still unsure? Ask again. Never invent a preference for the user.

## Anti-patterns

- Assuming “reasonable defaults” for product or design choices
- Expanding into full implementation detail before the direction is agreed
- Re-asking decisions that are already locked
- Starting large changes while clarification is still in progress
