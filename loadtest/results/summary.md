# Load Test Results — 2026-08-16

## In plain language

The pure calculator and the cached rate lookup each handle **~4,000 requests a
second** in a few milliseconds. Creating loans (the write side) handles
**~1,300 a second**. The portfolio report is the slowest — **~250 a second** —
because it adds up the whole portfolio on every call, so it gets slower as more
loans exist. That report is the first thing to optimise as data grows. No
requests failed under load on the healthy paths.

Load testing also **uncovered a bug**: the payment webhook crashed with a 500
(server error) on a request with a missing/garbage signature, instead of a clean
400 (rejection). That is now fixed.

## Setup

- **Target:** the local HA stack — 3 API replicas behind nginx on `:8080`
  (`docker-compose.ha.yml`). Numbers are aggregate across the three replicas.
- **Load:** k6, 20 peak virtual users, ~55s ramp (15s up / 30s hold / 10s down) per scenario.
- **Data store:** SQL Server 2022 in Docker (Azure SQL free tier is skipped — its
  auto-pause and compute quota would distort the numbers).
- Scenarios ran **sequentially**, so the command-path run created tens of
  thousands of loans *before* the query-path run measured the report — see the
  CQRS note below.

## Results

| Scenario | Endpoint | Throughput | avg | p95 | p99 | errors |
|---|---|---:|---:|---:|---:|---:|
| Amortization (pure CPU) | `POST /amortization/preview` | 4,053 req/s | 3.72 ms | 6.05 ms | 8.2 ms | 0% |
| Rate lookup (Redis cache) | `GET /rates/{type}/{term}` | 4,287 req/s | 3.55 ms | 6.88 ms | 11.33 ms | 0% |
| Command path (write / event store) | `POST /loans` | 1,334 req/s | 11.52 ms | 17.92 ms | 23.47 ms | 0% |
| Query path (read / aggregation) | `GET /reports/portfolio-summary` | 247 req/s | 61.94 ms | 134.66 ms | 180.96 ms | 0% |
| Payment webhook | `POST /webhooks/stripe` | 1,877 req/s | 8.16 ms | 19.61 ms | 36.94 ms | see note |

Every healthy scenario passed its k6 thresholds (error rate < 1%, p95/p99 within budget).

## Aggregate rehydration — snapshot vs full replay

Measured by `LoanRehydrationBenchmarkTests` against a 60-installment loan
(63 events, crossing the snapshot interval of 25 twice), 50 loads each:

| Load path | avg time |
|---|---:|
| With snapshot (replay only the tail past v50) | 1.748 ms |
| Full replay (no snapshot, all 63 events) | 2.381 ms |
| **Speedup** | **1.36×** |

At 63 events the win is modest — replaying a few dozen events is cheap CPU. This
confirms the domain note that typical streams (≤70 events) are already fast, and
is the input for the **snapshot-interval ADR**: snapshots earn their keep on
*much* longer streams; for this loan workload the interval mostly exercises the
mechanism rather than saving meaningful time.

## Findings

### 1. CQRS: the read side is not automatically the fast side
The write path (`POST /loans`, 1,334 req/s) out-throughput the read path
(`GET /reports/portfolio-summary`, 247 req/s). That looks backwards until you see
that a *simple* read — the cached rate lookup — does 4,287 req/s. The report is
slow because it **aggregates the whole portfolio on every call**, and the
command-path run had just created tens of thousands of loans. The CQRS split
keeps reads off the write database, but an aggregation query still needs help to
scale. **Fix:** a covering index for the summary, a pre-aggregated/materialised
summary row updated by the projector, or a short-TTL cache on the report.

### 2. Webhook robustness bug (found by load testing)
A request with a **missing or malformed `Stripe-Signature` header** hit an
unhandled `NullReferenceException` in `EventUtility.ParseStripeSignature` and
returned **500** (leaking a stack trace under the dev exception page), because
the handler only caught `StripeException`. **Fixed:** the header presence is
checked before any work, and construction is wrapped to turn any verify/parse
failure into a **400** — a non-Stripe request never causes a server error.

### 3. Webhook fetches its signing secret from Vault on every request
`StripeWebhookEndpoints` calls `ISecretProvider.GetSecretAsync("StripeWebhookSecret")`
per request — a Vault round-trip in the hot path that caps webhook throughput.
**Fix (future):** cache the signing secret in memory with a short TTL.

### 4. Harness note — synthetic Stripe signatures
`webhook.js` signs events with the webhook secret, but the synthetic signature
did not validate end-to-end against the Stripe SDK in this run (responses were
4xx), so its throughput reflects the endpoint's fixed cost (raw-body read +
per-request Vault fetch + HMAC computation) rather than a fully-processed event.
Getting the synthetic signature to validate is a follow-up.

## How to reproduce

See [`../README.md`](../README.md). In short, with the stack up:

```bash
k6 run -e BASE_URL=http://localhost:8080 loadtest/amortization.js   # + rates / command-path / query-path
dotnet test --filter FullyQualifiedName~Rehydration -l "console;verbosity=detailed"
```
