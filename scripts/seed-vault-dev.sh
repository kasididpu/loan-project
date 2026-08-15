#!/usr/bin/env sh
# Seeds the dev-mode Vault with the local development secrets — run once
# after `docker compose up -d`, and safe to re-run: patch only touches the
# listed keys, so anything added separately (e.g. Stripe keys) survives.
# Every value here is the same non-secret local default docker-compose uses.
docker exec \
  -e VAULT_ADDR=http://127.0.0.1:8200 \
  -e VAULT_TOKEN="${VAULT_DEV_ROOT_TOKEN_ID:-loan-dev-root}" \
  loan-vault \
  sh -c 'vault kv patch secret/loan-api \
    LoanDb="Server=localhost,1433;Database=LoanDb;User Id=sa;Password=LoanDev!Passw0rd;TrustServerCertificate=True" \
    Mongo="mongodb://root:LoanDevMongo1@localhost:27017" \
    RabbitMq="amqp://guest:guest@localhost:5672" \
    Redis="localhost:6379" \
  || vault kv put secret/loan-api \
    LoanDb="Server=localhost,1433;Database=LoanDb;User Id=sa;Password=LoanDev!Passw0rd;TrustServerCertificate=True" \
    Mongo="mongodb://root:LoanDevMongo1@localhost:27017" \
    RabbitMq="amqp://guest:guest@localhost:5672" \
    Redis="localhost:6379"'
