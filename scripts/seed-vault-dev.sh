#!/usr/bin/env sh
# Seeds the dev-mode Vault with the local development secrets — run once
# after `docker compose up -d`. Every value here is the same non-secret
# local default that docker-compose already uses; real environments seed
# their own values and never run this script.
docker exec \
  -e VAULT_ADDR=http://127.0.0.1:8200 \
  -e VAULT_TOKEN="${VAULT_DEV_ROOT_TOKEN_ID:-loan-dev-root}" \
  loan-vault \
  vault kv put secret/loan-api \
    LoanDb="Server=localhost,1433;Database=LoanDb;User Id=sa;Password=LoanDev!Passw0rd;TrustServerCertificate=True" \
    Mongo="mongodb://root:LoanDevMongo1@localhost:27017"
