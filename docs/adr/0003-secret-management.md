# ADR 0003 — Secret management via Vault behind ISecretProvider

**Status:** Accepted

## Context

The repository will become public, so no real secret may ever enter it — not in
code, not in `appsettings`, not in git history. Runtime needs real secrets anyway:
the Stripe key, connection strings (which carry passwords), the JWT signing key,
the field-encryption key.

## Decision

Every runtime secret is read from **HashiCorp Vault** through a single
`ISecretProvider` interface. Application code sees only the interface and a secret
*name* — never a value.

- `appsettings.json` holds **placeholders and non-secret local defaults only** (e.g. the dev SA password, which is a documented non-secret default).
- **Two layers, kept separate:**
  - *Runtime secrets* (Stripe key, connection strings) → Vault only.
  - *Deploy credentials* → CI's secret store, only if a deploy needs them (this local-first showcase has no deploy, so there are none).
- **Per-environment documents:** host dev reads `secret/loan-api` (localhost connection strings); the containerized stack reads `secret/loan-docker` (service-name connection strings) via `Vault:BasePath`. Genuine secrets are identical in both.
- **Least privilege:** the AppRole policy grants read on exactly the app's own documents (`secret/{data,metadata}/loan-api` and `.../loan-docker`) and nothing else.
- **CI guards it:** `gitleaks` scans the whole history; the known non-secret dev defaults are allowlisted so a *real* leak is what fails the build.

## Production-facing design (deferred, not built here)

In a real deployment CI would authenticate to Vault with **OIDC**: GitHub issues a
short-lived token to the workflow, Vault verifies it and returns a scoped, short-TTL
Vault token — so no static credential lives anywhere. The deploy path was dropped
for the local-first scope, so this is documented as the intended design rather than
implemented. See [production-migration.md](../production-migration.md) (Vault → Azure Key Vault).

## Consequences

- **+** No secret in the repo or history; app code is oblivious to where secrets live; swapping Vault for Azure Key Vault is one `ISecretProvider` implementation.
- **−** The dev Vault runs in-memory, so a restart wipes it — re-run the seed script. The webhook fetches its signing secret from Vault per request (a hot-path round-trip noted as a future cache).

## AI guardrail

The agent building this project sees only `ISecretProvider` and secret *paths* —
never a real secret value, connection string, or unmasked financial data in any
prompt, code, log sample, or document.
