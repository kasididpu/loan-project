# Loan Management API (.NET 8)

A mini loan management API built around financial-services backend patterns: amortization schedules (reducing balance method), flat and effective (IRR) interest rates, late-payment fees, an event-sourced `Loan` aggregate with a validated state machine, CQRS with separate write/read databases synced over Redpanda, and real Stripe (Test Mode) payment integration with webhook signature verification and idempotent processing.

> **Status: work in progress.** Built phase by phase; every phase lands as reviewed, tested commits. Nothing here is documented before it exists.

## Roadmap Progress

- [ ] Phase 1 — Domain & core logic (amortization, interest, event-sourced `Loan` + state machine)
- [ ] Phase 2 — Data layer (EF Core, event store, stored procedures, MongoDB)
- [ ] Phase 3 — Azure SQL Database compatibility
- [ ] Phase 3.5 — Secret management (Vault + `ISecretProvider`)
- [ ] Phase 4 — Stripe payment integration (Test Mode)
- [ ] Phase 4.5 — Internal MCP server
- [ ] Phase 5 — Async processing, event dispatcher, reconciliation/settlement
- [ ] Phase 6 — CQRS write/read databases + reporting
- [ ] Phase 7 — KYC/AML rules
- [ ] Phase 8 — Auth, authorization & data protection
- [ ] Phase 9 — High availability (API replicas)
- [ ] Phase 10 — Performance & load testing
- [ ] Phase 11 — DevOps, CI/CD & observability
- [ ] Phase 12 — Documentation

## Tech Stack

| Area | Choice |
|---|---|
| API | .NET 8 Web API, Clean Architecture |
| Relational DB | SQL Server (dev, Docker) → Azure SQL Database (deploy) |
| Event sourcing | Append-only event store + snapshots, `Loan` aggregate only |
| Event streaming | Redpanda (Kafka-compatible) — CQRS sync |
| Messaging | RabbitMQ — async tasks |
| Cache | Redis |
| Audit log | MongoDB |
| Payments | Stripe (Test Mode) — webhooks, signature verification, idempotency |
| Secrets | HashiCorp Vault behind an `ISecretProvider` interface |
| Observability | Serilog → Seq |
| Testing | xUnit + Moq; k6 for load testing |
| CI/CD | GitHub Actions + self-hosted ARM64 runner (Raspberry Pi 5, k3s) |

## Environments

**Windows** (dev — SQL Server in Docker, x86) → **Raspberry Pi 5** (deploy — ARM64, k3s) → **Azure SQL Database** (replaces SQL Server on the deploy path).

## Run It Locally

> Not runnable yet — Phase 1 has not landed. This section is filled in as the pieces become real.

The target developer experience, kept as a hard requirement throughout:

1. `git clone` and `docker compose up` — all backing services (SQL Server, Redis, RabbitMQ, Redpanda, MongoDB, Seq, Vault) start locally.
2. Seed Vault (dev mode) and the database with the provided scripts.
3. Open Swagger and exercise the full flow: create loan → approve → payment webhook → query status → reports.

No Azure account or paid service is required to run locally; Stripe integration uses Test Mode, which is free to set up.

## Conventions

Project conventions (architecture, code style, money-handling rules, commit format) are in [`CLAUDE.md`](CLAUDE.md). Agent tool permissions used during development are in [`.claude/settings.json`](.claude/settings.json).
