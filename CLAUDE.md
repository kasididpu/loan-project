# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project Overview

A mini loan management API built with **.NET 8**, showcasing financial-services backend patterns:

- Loan domain logic: amortization schedules (reducing balance), flat/effective interest rates, late-payment fees
- **CQRS** with separate Write/Read databases, synced via **Redpanda** events
- **Scoped event sourcing**: the `Loan` aggregate is fully event-sourced — append-only event store with snapshots and optimistic concurrency; events reach Redpanda via an event-store-as-outbox dispatcher. Everything else is conventional CRUD/CQRS.
- Payment integration with **Stripe** (Test Mode) — webhooks, signature verification, idempotency
- Async processing with **RabbitMQ**, caching with **Redis**, audit logs in **MongoDB**
- Secret management with **HashiCorp Vault** behind an `ISecretProvider` interface
- Observability with **Serilog → Seq**; testing with **xUnit + Moq + k6**

**Local-first architecture:** the entire system runs on one dev machine through `docker compose` — that is the deliverable. The relational schema is kept **Azure SQL Database**-compatible so a cloud deployment path remains open as an optional final step.

## Language Policy

All repository artifacts are **English only**: code, comments, XML docs, commit messages, and every committed document. No exceptions.

## Workflow Rules

1. Work incrementally, **one phase at a time**; stop for user review before continuing. Never scaffold the whole project at once.
2. Money-related business logic: **explain the formula and get user confirmation before committing.** The user must fully understand every calculation.
3. **Payment-path changes always require human review before commit.**
4. Flag **SQL Server vs Azure SQL Database** feature differences immediately when found (e.g., cross-database queries, SQL Agent jobs are unavailable on the PaaS side) — the optional cloud path must stay viable.

## Architecture & Code Style

### Structure

- Clean Architecture: `Domain` / `Application` / `Infrastructure` / `Api`. The final structure is proposed and justified in Phase 1 — dependencies always point inward; `Domain` has no external dependencies.

### C# Conventions

- File-scoped namespaces, `<Nullable>enable</Nullable>`, implicit usings
- One public type per file
- Naming: `PascalCase` for public members/types, `camelCase` for locals/parameters, `_camelCase` for private fields
- Async methods carry the `Async` suffix
- `var` when the type is obvious from the right-hand side; explicit type otherwise

### OOP Rules

- **SOLID.** Depend on interfaces for every external resource: `ISecretProvider`, `ILoanRepository`, `IPaymentGateway`, etc.
- **Rich domain model** — business rules live in entities and domain services, never in controllers.
- Constructor injection only; no service locator, no static state.
- Guard clauses over nested `if`s; composition over inheritance.

### Event Sourcing Rules (Loan aggregate only)

- Only the `Loan` aggregate is event-sourced. Everything else uses conventional CRUD/CQRS. **Never extend event sourcing to other aggregates without explicit user approval.**
- The event store is **append-only**: never write code or migrations that UPDATE or DELETE stored events. Event schema evolution happens by adding new event types.
- All state transitions are validated inside the aggregate; invalid transitions throw domain errors, and every valid and invalid transition is unit-tested.
- Optimistic concurrency via the `(AggregateId, Version)` unique constraint; conflicts are handled by reload-and-retry.
- Consumers and projections must be idempotent — dedupe by `(AggregateId, Version)`.
- The dispatcher (event store → Redpanda) runs as a single active instance; guarantee is at-least-once.

### Money Rules (non-negotiable)

- **`decimal` only.** `float`/`double` are forbidden for any monetary value.
- Rounding is always explicit: state `MidpointRounding` at every call site and document the chosen strategy.
- Every money calculation is unit-tested, including edge cases: final installment, overpayment, underpayment.

### Comments

- English only. Explain **why**, not what.
- Required on index and stored-procedure design decisions (query pattern → index shape reasoning).

## Testing

- xUnit + Moq; naming: `MethodName_Scenario_ExpectedResult`
- Arrange-Act-Assert layout
- Domain logic is tested without infrastructure (no DB, no network)
- `dotnet test` must pass before every commit

## Git Conventions

Apply from the very first commit once git is initialized.

### Commit Messages — Conventional Commits

```
<type>(<scope>): <subject>

<body — explains why, when non-obvious>
```

- **Types:** `feat`, `fix`, `refactor`, `test`, `docs`, `chore`, `perf`, `ci`
- **Scopes:** `domain`, `application`, `data`, `api`, `payment`, `mcp`, `cqrs`, `auth`, `infra`, `ci`, `docs` (extend as new areas appear)
- Subject: imperative mood, lowercase, no trailing period, ≤ 72 characters

Examples:

```
feat(domain): add amortization schedule calculator
fix(payment): prevent duplicate webhook processing
docs: add CLAUDE.md with project conventions
```

### Branching

- `main` is always stable and is updated **only through pull requests** — never by local merges or direct pushes.
- One branch per phase: `feature/phase-<n>-<short-name>` (e.g., `feature/phase-1-domain`).
- The pull request is merged on GitHub after human review; delete the branch after merging.

### Commit Hygiene

- Atomic commits: one logical change per commit; every commit builds and passes tests.
- **No secrets in any commit, ever.** The repository will become public later and history cannot be cleaned cheaply. Secrets live in Vault behind `ISecretProvider`; `appsettings.json` contains placeholders and non-secret local defaults only.

## Security & AI Guardrails

- Never request, display, or write real secret values in prompts, code, logs, or documents — reference **Vault paths only**.
- All data in dev databases is **seed/test data**. No real customer PII or unmasked financial data anywhere.
- Stripe: **Test Mode only.** Live keys are never used.
