# Load Tests (Phase 10)

[k6](https://k6.io) scripts that measure the API under load and let the CQRS
write/read split be compared with numbers. Everything runs locally against the
dev stack — no cloud, no paid service.

## Prerequisites

1. Infrastructure up: `docker compose up -d`
2. Vault seeded: `sh scripts/seed-vault-dev.sh`
3. The API running — either the single dev instance or the HA stack:
   - single: `dotnet run --project src/LoanProject.Api` (listens on `:5213`)
   - HA: `docker compose -f docker-compose.yml -f docker-compose.ha.yml up -d --build` (nginx on `:8080`)
4. [k6 installed](https://k6.io/docs/get-started/installation/).

## Scenarios

| Script | Path exercised | Auth | Notes |
|---|---|---|---|
| `amortization.js` | `POST /amortization/preview` | none | pure CPU (money calc) — the baseline |
| `rates.js` | `GET /rates/{type}/{term}` | none | Redis cache-aside read |
| `command-path.js` | `POST /loans` | admin | CQRS write side (event store) |
| `query-path.js` | `GET /reports/portfolio-summary` | admin | CQRS read side (read DB) |
| `webhook.js` | `POST /webhooks/stripe` | signature | HMAC verify + per-request Vault secret fetch |

## Running

```bash
# defaults to http://localhost:5213 and 20 peak VUs
k6 run loadtest/amortization.js
k6 run loadtest/rates.js
k6 run loadtest/command-path.js
k6 run loadtest/query-path.js

# webhook needs the same secret the app reads from Vault (StripeWebhookSecret);
# skip it if Stripe test keys are not configured locally
k6 run -e WEBHOOK_SECRET=$StripeWebhookSecret loadtest/webhook.js

# overrides: point at the HA stack, push more load, save a JSON summary
k6 run -e BASE_URL=http://localhost:8080 -e VUS=50 loadtest/amortization.js
k6 run --summary-export results/amortization.json loadtest/amortization.js
```

Each script sets pass/fail `thresholds` (error rate + p95/p99 latency), so a run
exits non-zero on a regression.

## Results

Captured runs and the written analysis live in [`results/`](results/) —
`summary.md` explains what the numbers mean in plain language.
