# ADR 0002 — CQRS with separate write and read databases

**Status:** Accepted

## Context

The write side is an append-only event ledger optimised for correctness and
concurrency. Reads are a different shape — a loan's current status, portfolio
aggregates for reporting — that would be awkward and contended if computed from
the ledger on every request.

## Decision

Split reads from writes into **two physically separate SQL databases**:

- **Write DB** — the event store (+ CRUD tables). Commands append here.
- **Read DB** — denormalised projections. The `LoanReadModelProjector` consumes the `loan-events` topic and folds each event into read tables; queries read only here.
- The two are synced **only through projected events** — never a cross-database query or join (Azure SQL Database has none, so this keeps the cloud path viable).

## Consequences

- **Eventually consistent.** A write and its read are not guaranteed visible in the
  same instant; the projector catches up asynchronously. Endpoints and the Bruno
  collection call this out ("retry after a moment"). This is the accepted trade-off
  — the read side can be queried and scaled without contending with writes.
- **The read side is not automatically the fast side.** Phase 10 load testing found
  the portfolio-summary aggregation was the slowest path (~247 req/s vs ~1,334 req/s
  for the write path and ~4,000 for a cached read) because it aggregates the whole
  portfolio per call and grows with data volume. CQRS isolates reads from writes; it
  does **not** make an aggregation query fast on its own — that still needs an index,
  a materialised summary, or a cache. Recorded as a known optimisation.
- Consumers/projections are **idempotent** (dedupe by `(AggregateId, Version)`), so
  at-least-once delivery from the dispatcher is safe to replay.

## Alternatives considered

- **Single database, read models as views** — simpler, but couples read scaling to the write DB and loses the clean Azure-SQL-friendly separation.
- **Synchronous projection inside the command** — removes eventual consistency but reintroduces a dual-write and puts projection latency on the write path.
