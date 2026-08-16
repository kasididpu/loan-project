# Execution Plan Evidence — Payment Indexes

Before/after proof for the two indexes added in `AddPaymentIndexes`
(Phase 2), captured with `SET STATISTICS PROFILE ON` against 100,000
synthetic payments on the local SQL Server dev container.

## Files

| File | Purpose |
|---|---|
| `01-generate-volume.sql` | Inserts 100k synthetic payments (deterministic ids — a rerun collides on the PK instead of duplicating). Requires SQL Server 2022+ (`GENERATE_SERIES`). |
| `02-capture-plan.sql` | The two real query patterns, instrumented with `STATISTICS PROFILE`. |
| `03-before-index.txt` | Plans before any nonclustered index existed. |
| `04-after-index.txt` | Plans after `AddPaymentIndexes` was applied. |

## Query A — statement view (`WHERE LoanId = @id ORDER BY PaidAtUtc`)

This is `PaymentRepository.ListByLoanAsync`, the query
`IX_Payment_LoanId_PaidAtUtc` was shaped for: the leading column turns
the scan into a seek, the second column hands rows back pre-sorted.

| | Before | After |
|---|---|---|
| Access | Clustered Index Scan — reads all 100,000 rows | Index Seek — reads the loan's 100 rows |
| Ordering | explicit `Sort` operator | gone (`ORDERED FORWARD`) |
| Cost | 1.1128 | **0.3161** |

The after-plan contains a Key Lookup (`Clustered Index Seek … LOOKUP`,
Executes = 100) fetching `Amount` and `StripeEventId`, which the index
does not carry. **Deliberately not covered with `INCLUDE`**: a hundred
cheap lookups per query do not justify taxing every future INSERT with
a wider index at this scale.

## Query B — end-of-day aggregation (date range + `GROUP BY LoanId`)

Unchanged by design: the new index leads with `LoanId`, so a
`PaidAtUtc` range cannot use it (cost 1.177 before and after). No
third index was added — the end-of-day summary runs once per day and a
scan at this volume is acceptable; choosing *not* to tune, with the
reasoning written down, is part of the tuning discipline. Revisit with
real load numbers in the performance phase.

## Second index in the same migration

`UX_Payment_StripeEventId` (unique) is not a performance index — it is
the mechanical guard for webhook idempotency (one Stripe event, one
payment), the same referee philosophy as `UQ_EventStore_AggVer` on the
event store.
