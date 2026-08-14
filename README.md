# Loan Management API (.NET 8)

A mini loan management API built around financial-services backend patterns: amortization schedules (reducing balance method), flat and effective (IRR) interest rates, late-payment fees, an event-sourced `Loan` aggregate with a validated state machine, CQRS with separate write/read databases synced over Redpanda, and real Stripe (Test Mode) payment integration with webhook signature verification and idempotent processing.

> **Status: work in progress.** Built phase by phase; every phase lands as reviewed, tested commits. Nothing here is documented before it exists.

## Roadmap Progress

- [x] Phase 1 — Domain & core logic (amortization, interest, event-sourced `Loan` + state machine)
- [x] Phase 2 — Data layer (EF Core, event store, stored procedures, MongoDB)
- [ ] Phase 3.5 — Secret management (Vault + `ISecretProvider`)
- [ ] Phase 4 — Stripe payment integration (Test Mode)
- [ ] Phase 5 — Async processing, event dispatcher, reconciliation/settlement
- [ ] Phase 6 — CQRS write/read databases + reporting
- [ ] Phase 7 — KYC/AML rules
- [ ] Phase 8 — Auth, authorization & data protection
- [ ] Phase 9 — High availability (API replicas, local compose)
- [ ] Phase 10 — Performance & load testing
- [ ] Phase 11 — DevOps, CI & observability
- [ ] Phase 12 — Documentation
- [ ] Optional final step — cloud deployment path (Azure SQL Database compatibility)

## Tech Stack

| Area | Choice |
|---|---|
| API | .NET 8 Web API, Clean Architecture |
| Relational DB | SQL Server (Docker); schema kept Azure SQL Database–compatible |
| Event sourcing | Append-only event store + snapshots, `Loan` aggregate only |
| Event streaming | Redpanda (Kafka-compatible) — CQRS sync |
| Messaging | RabbitMQ — async tasks |
| Cache | Redis |
| Audit log | MongoDB |
| Payments | Stripe (Test Mode) — webhooks, signature verification, idempotency |
| Secrets | HashiCorp Vault behind an `ISecretProvider` interface |
| Observability | Serilog → Seq |
| Testing | xUnit + Moq; k6 for load testing |
| CI | GitHub Actions (build, test, secret scan) |

## Environment

**Local-first by design.** The entire system runs on one machine with Docker Desktop — that is the deliverable, and anyone who clones the repo gets the same experience. The relational schema is kept Azure SQL Database–compatible, so a cloud deployment path remains open as an optional final step.

## Run It Locally

Requires Docker Desktop and the .NET 8 SDK.

```bash
git clone <this repo> && cd loan-project
docker compose up -d      # SQL Server 2022 + MongoDB 8 (more services join in later phases)

dotnet tool install --global dotnet-ef
dotnet ef database update --project src/LoanProject.Infrastructure --startup-project src/LoanProject.Api

dotnet test               # domain unit tests + integration tests against both containers
dotnet run --project src/LoanProject.Api   # dev startup seeds sample data, Swagger at /swagger
```

The dev seed creates two customers and one event-sourced loan with real
history in the ledger (originated → approved → disbursed → first
installment paid), so both worlds have data to explore immediately.

The target end state, kept as a hard requirement throughout: `docker
compose up` starts every backing service (Redis, RabbitMQ, Redpanda, Seq
and Vault join in their phases), and no Azure account or paid service is
ever required to run locally — Stripe integration uses Test Mode, which
is free to set up.

## Conventions

Project conventions (architecture, code style, money-handling rules, commit format) are in [`CLAUDE.md`](CLAUDE.md). Agent tool permissions used during development are in [`.claude/settings.json`](.claude/settings.json).
