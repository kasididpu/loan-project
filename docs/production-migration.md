# Production migration path (local → Azure)

The system is a **local-first showcase** — the deliverable runs entirely on
`docker compose`. It was, however, built so a cloud deployment stays a
configuration change, not a rewrite: every external resource sits behind an
interface, and the relational schema is kept Azure SQL Database–compatible
throughout (no cross-database queries, no SQL Agent — scheduled jobs run in-app on
Quartz for exactly this reason).

## Service mapping

| Local (docker compose) | Azure | Same concept | What differs / code impact |
|---|---|---|---|
| SQL Server (2 databases) | **Azure SQL Database** ×2 | relational store, T-SQL | schema already compatible; connection string only. Native HA/replicas are managed by the platform |
| Redpanda | **Azure Event Hubs** (Kafka endpoint) | ordered event log | Kafka-compatible endpoint → bootstrap address + auth change; producer/consumer code unchanged |
| RabbitMQ | **Azure Service Bus** | async work queue | swap the `IPaymentNotifier` / consumer implementation; the port interface stays |
| Quartz.NET (in-app) | **Azure Functions (timer trigger)** | scheduled jobs | the reconciliation/settlement *handlers* are already separate classes; a Function just invokes them on a CRON trigger |
| Redis | **Azure Cache for Redis** | cache-aside | connection string only |
| MongoDB | **Azure Cosmos DB (Mongo API)** | document store | connection string only (verify indexes) |
| HashiCorp Vault | **Azure Key Vault** | secret store | one new `ISecretProvider` implementation; call sites unchanged |
| Seq | **Application Insights** | structured logs/traces | swap the Serilog sink; log statements unchanged |
| nginx | **Application Gateway / Ingress** | load balancer | infra config, not app code |
| Stripe (Test Mode) | Stripe (Live) | payments | key swap only; **live keys are never used in this project** |

## Why the code barely moves

Each external resource is reached through a port interface (`ISecretProvider`,
`ILoanRepository`, `IPaymentNotifier`, `IInterestRateLookup`, the query
interfaces). Moving to Azure is mostly: (1) point connection strings at managed
services, (2) provide a Key Vault–backed `ISecretProvider`, (3) swap the Service
Bus/Event Hubs client implementations. Domain and Application code — where the
business value is — does not change.

## What would need real work

- **Scheduled jobs → Functions:** the *trigger* moves out of the app process, but the handlers are already isolated, so it's wiring, not rewriting.
- **Secret auth:** production Vault/Key Vault access should use OIDC/managed identity (short-lived tokens), not the dev root token — see [adr/0003-secret-management.md](adr/0003-secret-management.md).
- **The event dispatcher stays single-active** (see [adr/0004-high-availability.md](adr/0004-high-availability.md)); in Azure that means a single worker/Function instance for the outbox drain, or a lease.
