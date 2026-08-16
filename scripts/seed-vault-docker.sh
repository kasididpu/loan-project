#!/usr/bin/env sh
# Seeds the dev Vault with the CONTAINER-facing secret document (secret/loan-docker)
# for the Phase 9 HA stack. It mirrors seed-vault-dev.sh, but the connection
# strings point at compose SERVICE NAMES (sqlserver, mongodb, ...) instead of
# localhost, because the API/worker containers reach the infra over the compose
# network. Genuine secrets (keys, passwords, Stripe) are identical to the host
# document. Run once after `docker compose -f docker-compose.yml -f docker-compose.ha.yml up -d`.
#
# Safe to re-run: kv patch touches only the listed keys. The dev Vault keeps
# secrets in memory, so a Vault container restart wipes them — re-run then.

VAULT_TOKEN_VALUE="${VAULT_DEV_ROOT_TOKEN_ID:-loan-dev-root}"
SA_PASSWORD="${MSSQL_SA_PASSWORD:-LoanDev!Passw0rd}"
MONGO_PASSWORD="${MONGO_ROOT_PASSWORD:-LoanDevMongo1}"

# Base secrets — service-name connection strings + the same non-secret dev keys.
# patch-or-put so it works whether the document already exists or was wiped.
docker exec \
  -e VAULT_ADDR=http://127.0.0.1:8200 \
  -e VAULT_TOKEN="$VAULT_TOKEN_VALUE" \
  -e SA_PASSWORD="$SA_PASSWORD" \
  -e MONGO_PASSWORD="$MONGO_PASSWORD" \
  loan-vault \
  sh -c 'set -- \
    LoanDb="Server=sqlserver,1433;Database=LoanDb;User Id=sa;Password=$SA_PASSWORD;TrustServerCertificate=True" \
    LoanReadDb="Server=sqlserver,1433;Database=LoanReadDb;User Id=sa;Password=$SA_PASSWORD;TrustServerCertificate=True" \
    Mongo="mongodb://root:$MONGO_PASSWORD@mongodb:27017" \
    RabbitMq="amqp://guest:guest@rabbitmq:5672" \
    Redis="redis:6379" \
    JwtSigningKey="loan-dev-jwt-signing-key-change-me-0123456789abcdef" \
    FieldEncryptionKey="loan-dev-field-encryption-key-change-me" \
    DevSeedUserPassword="Dev!Passw0rd" \
    DevOAuthClientSecret="dev-oauth-client-secret-change-me"; \
    vault kv patch secret/loan-docker "$@" || vault kv put secret/loan-docker "$@"'

# Stripe keys — restored from the gitignored local file if present, same as the
# host seeder. Stripe endpoints are external, so the values match both documents.
SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
SECRETS_FILE="$SCRIPT_DIR/vault-secrets.local.env"
if [ -f "$SECRETS_FILE" ]; then
  # shellcheck source=/dev/null
  . "$SECRETS_FILE"
  if [ -n "$StripeSecretKey" ] && [ -n "$StripeWebhookSecret" ]; then
    docker exec \
      -e VAULT_ADDR=http://127.0.0.1:8200 \
      -e VAULT_TOKEN="$VAULT_TOKEN_VALUE" \
      -e StripeSecretKey="$StripeSecretKey" \
      -e StripeWebhookSecret="$StripeWebhookSecret" \
      loan-vault \
      sh -c 'vault kv patch secret/loan-docker \
        StripeSecretKey="$StripeSecretKey" \
        StripeWebhookSecret="$StripeWebhookSecret"'
    echo "Restored Stripe keys into secret/loan-docker from $SECRETS_FILE"
  else
    echo "WARN: $SECRETS_FILE exists but StripeSecretKey/StripeWebhookSecret are unset; skipped Stripe keys."
  fi
else
  echo "No $SECRETS_FILE (see vault-secrets.local.env.example); skipped Stripe keys."
fi
