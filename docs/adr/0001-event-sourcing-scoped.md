# ADR 0001 — Event sourcing, scoped to the Loan aggregate

**Status:** Accepted

## Context

A loan's history *is* its business meaning: who approved it, when it was disbursed,
every payment, whether it defaulted. Regulators and disputes need that trail intact.
Most of the system, though, is ordinary CRUD (customers, a payment record, audit
log) where the current row is all anyone needs.

## Decision

Event-source **only the `Loan` aggregate**. Everything else stays conventional
CRUD/CQRS.

- The event store is **append-only** — no code or migration ever UPDATEs or DELETEs a stored event. Schema evolves by adding new event types.
- All state transitions are validated **inside the aggregate**; invalid transitions throw domain errors and are unit-tested (valid and invalid).
- Optimistic concurrency via a `(AggregateId, Version)` unique constraint; conflicts reload-and-retry.
- The store **doubles as the outbox**: a single dispatcher reads rows past a persisted cursor and publishes them to Redpanda, then advances the cursor. A crash between publish and advance re-publishes — never loses — so delivery is at-least-once and consumers dedupe by `(AggregateId, Version)`.
- **Snapshots** every 25 events: load reads the latest snapshot, then replays only the tail.

## Why only Loan

Event sourcing adds real cost (serialization registry, snapshots, replay, projections).
It earns that cost where an audit trail is the product — the loan lifecycle — and
not where a mutable row is simpler and sufficient (a customer's address). Spreading
it system-wide would be cost without payoff. **Extending it to another aggregate
requires an explicit decision, not drift.**

## Snapshot interval — measured, not guessed

Phase 10 measured rehydration of a 60-installment loan (63 events, crossing the
interval twice), 50 loads each:

| Load path | avg |
|---|---:|
| With snapshot (replay the tail) | 1.75 ms |
| Full replay (all 63 events) | 2.38 ms |
| Speedup | **1.36×** |

Replaying a few dozen events is cheap, so at realistic stream lengths (≤70 events
for a normal loan) the interval mostly exercises the mechanism. Snapshots earn
their keep on *much* longer streams; the interval is deliberately conservative and
backed by a number, and the benchmark also asserts snapshot-load == full-replay
(the cache must never diverge from the ledger).

## Consequences

- **+** Complete, tamper-evident audit trail; the read side and the outbox both derive from one source of truth; no dual-write between DB and broker.
- **−** More moving parts than CRUD; reads of the aggregate go through replay (mitigated by snapshots); consumers must be idempotent.

## Deliberately not done

- **Upcasting / event versioning framework** — schema evolves by adding event types; no in-place event rewriting (would fight append-only).
- **Temporal / as-of query API** — replay exists for rehydration, not exposed as a time-travel query surface.
- **Event sourcing beyond Loan** — see above.
