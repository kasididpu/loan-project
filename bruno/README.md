# Bruno collection

A portable API collection for the **headline loan flow** — import it in [Bruno](https://www.usebruno.com/)
(open-source, no account, and every request is a plain-text `.bru` file that diffs
in git). Works outside VS Code, unlike `../LoanProject.http`.

This is a focused happy-path tour, not the full surface: the remaining endpoints
(MFA/OAuth login, customer onboarding + KYC, reject, the signed Stripe webhook,
rates, daily-collections) are exercised in [`../LoanProject.http`](../LoanProject.http),
in **Swagger** (`/swagger`), and by the integration tests.

## Use it

1. Start the stack and the app (see the root `README.md`): `docker compose up -d`, seed Vault, `dotnet run --project src/LoanProject.Api`.
2. Open Bruno → **Open Collection** → this `bruno/` folder.
3. Select the **Local** environment (top right). It points at `http://localhost:5213`; change `baseUrl` to `http://localhost:8080` to drive the HA stack through nginx.
4. Run the requests in order (`01` → `08`). `01-login` and `02-originate-loan` save the bearer token and the new loan id as collection variables, so the later requests just work.

## The flow

| # | Request | What it shows |
|---|---|---|
| 01 | Login | password login → bearer token (saved) |
| 02 | Originate loan | CQRS command → event store (loan id saved) |
| 03 | Approve loan | state transition (needs KYC-verified customer) |
| 04 | Disburse loan | state transition → loan becomes Active |
| 05 | Loan status | CQRS read side (Read DB, eventually consistent) |
| 06 | Loan events | the append-only audit trail (event stream) |
| 07 | Portfolio report | aggregate query over the Read DB |
| 08 | Amortization preview | stateless money calc (anonymous) |
