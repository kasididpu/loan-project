# ADR 0004 — High availability via a web/worker split

**Status:** Accepted

## Context

The API should scale horizontally for availability. But the process also runs
background singletons that must **not** be duplicated: the event dispatcher (it
advances one persisted cursor — two would race it and double-publish), the Quartz
jobs (reconciliation and settlement — two would settle twice), and the read-model
projector.

## Decision

One image, three roles selected by `App:Role`:

- `api` — serves HTTP only; runs **no** background work. Scaled to N replicas behind nginx.
- `worker` — exactly one instance; owns the dispatcher, projector, payment consumer, Quartz jobs, and the dev DB migrate/seed.
- `all` (default) — both, so a plain `dotnet run` is unchanged for local dev.

Supporting pieces:

- **Health probes:** `/health/live` (process up) and `/health/ready` (verifies write DB, read DB, Redis, Mongo, RabbitMQ). The load balancer routes on readiness; probes live in `Infrastructure` and reuse the DI clients.
- **Load balancer:** nginx round-robin with *passive* health checks (`max_fails`/`fail_timeout`) + `proxy_next_upstream` — open-source nginx has no active checks, and passive is enough to route around a dead replica. An `X-Upstream-Addr` header makes the routing visible.
- **Migration is the worker's job** (one process), so replicas never race EF migrations on a fresh database.

## Consequences

- **+** API scales out for availability; singletons stay single, so nothing double-fires; failover is transparent to clients.
- **−** The worker is a single point for background work (acceptable: its jobs are idempotent/resumable, and the dispatcher's cursor makes at-least-once safe). The worker migrates before it serves, so its own health goes green a little later on a fresh database.

## Evidence (observed 2026-08-16)

Three replicas round-robin normally; `docker stop loan-api2` under traffic → **8/8
requests still 200**, nginx dropped the dead replica from rotation (upstream fell to
two IPs), and `docker start` rejoined it. CI additionally smoke-tests the stack:
`/health/ready` through nginx must return 200.
