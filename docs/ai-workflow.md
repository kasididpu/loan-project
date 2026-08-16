# AI workflow

This project was built with an AI coding agent (Claude Code) under a deliberate
workflow, not free-form prompting. The point was not "let the AI write it" but
"drive the AI with tight scope, then verify everything" — the same discipline a
team would apply to any contributor.

## How work was scoped and driven

- **A written roadmap + a conventions file** (`CLAUDE.md`) are the agent's standing
  brief: architecture rules, money rules (`decimal` only, explicit rounding), event-
  sourcing rules, git conventions. The agent reads them before each phase.
- **One phase at a time, one PR per phase.** The agent works a phase, opens a PR,
  and stops. It never scaffolds the whole system at once.
- **The human owns `main`.** The agent may push feature branches and open PRs but
  **never merges** — every change enters `main` only through a human-reviewed,
  human-merged PR. The review gate is a person reading the diff on GitHub.
- **Verify, don't trust.** Each phase must pass, in order: `dotnet build` → `dotnet
  test` → a hands-on demo (real requests against the running stack) → and from
  Phase 11, CI on a clean runner. Money formulas are explained and confirmed with
  the human *before* commit; any change to the payment path requires explicit human
  review before commit.

## Guardrails (enforced throughout)

- **Secrets live in Vault behind `ISecretProvider`.** The agent sees only the
  interface and secret *names* — never a real key, connection string, or unmasked
  financial value in any prompt, code, log sample, or document.
- **All dev-database data is seed/test data.** No real customer PII anywhere.
- **Stripe is Test Mode only.** Live keys are never used.
- **Payment-path changes require human sign-off** before commit — mirroring a real
  team's "payment changes always require human review".

## One example where the agent got it wrong — and how it was caught

**What happened (Phase 8, audit correctness).** The `Loan` aggregate's lifecycle
events needed to record *who* approved/disbursed/rejected a loan. The agent added
the acting user's id as an **optional, nullable, trailing** parameter
(`Guid? approvedByUserId = null`) — specifically so the existing tests and call
sites wouldn't have to change.

**Why that's wrong.** In a financial audit trail, "who performed this action" is
not optional — approving a loan without recording the actor is a defect, not a
convenience. Letting *test churn* dictate the domain signature is backwards: the
design should lead and the tests should follow.

**How it was caught.** The human reviewer, reading the change, pushed back directly:
the actor id must be **required** (non-nullable) and placed **before** the display
name in every method, and the tests must be updated to match — "you can't let the
tests drive the design." The agent had optimized for not touching tests; the human
optimized for correctness.

**The fix.** The actor id became a required `Guid`, positioned before the name, on
every command and event; all call sites and tests were updated; the invalid
"no actor" path was removed entirely.

**The lesson (now a standing rule):** avoiding test churn is never a design reason.
Tests follow the design, not the other way around.

## How verification catches agent over-confidence (two more, briefly)

- **Load/fuzz testing found a crash on the payment path (Phase 10).** The Stripe
  webhook returned **500** (an unhandled `NullReferenceException`) on a request with
  a missing/garbage signature, because the handler caught only `StripeException`.
  Normal tests and demos (which send *valid* signatures) never hit it; a k6 load run
  plus a malformed request did. Fixed to reject any non-Stripe request with a 400.
- **CI on a clean runner found three latent bugs a long-lived local database had
  hidden (Phase 11).** Locally the dev database was always already migrated, so every
  `Migrate()` was a no-op and the "build from scratch" path was never exercised. On a
  fresh CI database: a migration race between parallel test assemblies, tests that
  opened a raw connection before the schema existed, and the app and tests encrypting
  shared seed data with different keys — all surfaced and were fixed. The agent had
  reported the work "green" from local runs; a clean environment proved otherwise.

The recurring theme: the agent is fast and usually right, but it will confidently
declare success. The value is in the *gates* — human review of the diff, a real
demo, and CI on a clean environment — that catch the cases where fast-and-usually-
right isn't right.
