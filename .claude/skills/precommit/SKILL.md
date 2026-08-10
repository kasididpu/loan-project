---
name: precommit
description: Use when the user invokes /precommit, or before creating any git commit in this repository.
---

# Precommit — Commit Gate

Run every gate in order. Any gate fails → STOP, report which gate and why, and wait. Do not commit.

## Gates

1. **Scope** — run `git status` + `git diff` (staged and unstaged). List exactly what would be committed. Must be one logical change (atomic commit); if it is several, propose the split.
2. **Money logic** — if the diff touches interest, amortization, fees, balances, or rounding: the formula must already have been explained to the user and confirmed in this session (CLAUDE.md rule). Not yet → stop and explain first.
3. **Payment path** — if the diff touches webhook, payment, or Stripe code: explicit human review approval is required before commit.
4. **Build + tests** — ask the user to run `! dotnet build` then `! dotnet test`, and read the results from the chat output. Any failure → stop. (Skip only while the repo has no .NET solution yet.)
5. **Secrets** — scan the diff for connection strings, API keys, tokens, or anything credential-shaped. `appsettings.json` may contain placeholders and non-secret defaults only; Vault path references are fine.
6. **Message** — Conventional Commits: `type(scope): subject`, imperative, lowercase, ≤72 chars; body only when the why is non-obvious; English; **no trailers of any kind**.
7. **Commit** — create the commit, then show `git log -1 --stat` for review. Never push; never create a PR.
