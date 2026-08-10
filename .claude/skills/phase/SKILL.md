---
name: phase
description: Use when the user invokes /phase <n> or asks to start or resume a roadmap phase.
---

# Phase — Start a Roadmap Phase

ARGUMENTS: the phase number from the roadmap (e.g. `1`, `3.5`, `4.5`).

## Steps

1. Read, in order: the current roadmap in `notes/` (v5 — the section for this phase plus the global notes/guardrails at the top and bottom), `CLAUDE.md`, and `CLAUDE.local.md`.
2. Summarize back to the user : the phase goals, task list, acceptance criteria, items marked **(v5)**, and phase-specific warnings that apply (ARM64 image checks, SQL Server vs Azure SQL differences, secrets-in-Vault-only, docs/ currently gitignored).
3. Propose the branch name `feature/phase-<n>-<short-name>` and wait for the user to confirm before creating it.
4. Propose the work as small, reviewable increments — one slice at a time, never the whole phase at once.
5. **Hidden-classroom overlay (Phases 1–2):** stop and explain every first-time C# construct (then record it in `notes/glossary-csharp.html`) and every piece of EF Core-generated SQL (then record it in `notes/glossary-tsql.html`).
6. Wait for user approval of the plan before writing any code.
