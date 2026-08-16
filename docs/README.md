# Documentation

Design and architecture docs for the loan-management API.

- **[architecture.md](architecture.md)** — layers, the local-first system, the CQRS + event flow, and why each component was chosen (with diagrams).
- **Architecture Decision Records** — the *why* behind the key choices:
  - [0001 — Event sourcing, scoped to Loan](adr/0001-event-sourcing-scoped.md)
  - [0002 — CQRS write/read split](adr/0002-cqrs-read-write-split.md)
  - [0003 — Secret management (Vault)](adr/0003-secret-management.md)
  - [0004 — High availability (web/worker)](adr/0004-high-availability.md)
- **[production-migration.md](production-migration.md)** — how the local stack maps to Azure, and why the code barely moves.
- **[ai-workflow.md](ai-workflow.md)** — how this was built with an AI agent, its guardrails, and one example where the agent got it wrong and how it was caught.
- **[execution-plans/](execution-plans/)** — Phase 2 evidence: SQL query-plan captures before/after indexing.
