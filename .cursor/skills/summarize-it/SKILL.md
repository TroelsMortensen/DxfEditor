---
name: summarize-it
description: >-
  Explain what was just done in a human-readable temp markdown under
  .cursor/summaries/, with why, classes/files touched, and diagrams when
  useful. Use after implementing a feature, when the user asks what changed,
  or when invoked as summarize-it.
---

# Summarize It

After substantive work, **explain what you just did**. Write a reviewable temp markdown file (same idea as Cursor plan-mode artifacts). Do not replan. Do not keep coding unless the user asks.

## Deliverable

1. **Primary:** a markdown file under `.cursor/summaries/`
2. **Chat:** path to that file + a 1–3 sentence headline only (full detail lives in the file)

## Temp file location and naming

1. Ensure `.cursor/summaries/` exists at the project root (create if missing).
2. Filename: short kebab slug from the work (e.g. `undo-redo-v1.md`).
3. If that name already exists, prefix with local date: `YYYYMMDD-undo-redo-v1.md`.
4. These are **session/review artifacts**, not sequential `issues/NNN` docs. Do not treat them as long-lived source of truth unless the user later asks to promote content into an issue.
5. Do **not** write into `issues/` unless the user explicitly asks to update the issue.

## What the summary must contain

Write for a **human reviewer**; the file must stand alone without the chat:

1. **Header** — title, date, link to related `issues/NNN-….md` and/or SRS when applicable
2. **What changed** — plain-language outcome (user-facing + technical)
3. **Why** — decisions and constraints that drove the approach (short; skip trivia)
4. **Touched surface** — classes, services, components, JS files, notable methods (grouped; markdown links to paths)
5. **Diagrams** — Mermaid when ownership or flow changed (flowchart and/or sequence)
6. **How to verify** — short manual test bullets when relevant
7. **Follow-ups** — known gaps / out of scope left undone (if any)

## Diagram standard

- Use Mermaid in fenced `mermaid` blocks when it clarifies the change.
- Prefer simple flowcharts for ownership/wiring; sequence diagrams for request → state flows.
- No spaces in node IDs; quote tricky labels; avoid reserved words like bare `end` as IDs.

## Agent workflow

1. Inspect what **actually** changed this session (diff, files edited, related issue). Do not invent work.
2. Write the summary `.md` with the Write tool (create/overwrite as naming rules allow).
3. Reply in chat with the file path and a short headline.
4. Stop. No new features, no design reopen, unless the user asks.

## Anti-patterns

- Chat-only dump with no file
- Writing into `issues/` without an explicit request
- Vague “refactored some stuff” without class/file names
- Summarizing planned-but-not-done work as completed
- Continuing to implement in the same turn as the summary
