#!/usr/bin/env sh
# Seeds the dev-mode Vault with the local development secrets — run once after
# `docker compose up -d`, and safe to re-run: `kv patch` only touches the listed
# keys. Every base value here is the same non-secret local default that
# docker-compose already uses.
#
# The dev Vault keeps secrets in memory, so a container restart wipes them all.
# This script also restores the Stripe TEST-MODE keys from the gitignored file
# scripts/vault-secrets.local.env (see vault-secrets.local.env.example) when it
# exists, so those keys survive restarts. Real key values never enter the repo.

VAULT_TOKEN_VALUE="${VAULT_DEV_ROOT_TOKEN_ID:-loan-dev-root}"

# Base secrets — non-secret local defaults. patch-or-put so it works whether the
# document already exists (normal re-run) or was wiped by a restart (recreate).
docker exec \
  -e VAULT_ADDR=http://127.0.0.1:8200 \
  -e VAULT_TOKEN="$VAULT_TOKEN_VALUE" \
  loan-vault \
  sh -c 'vault kv patch secret/loan-api \
    LoanDb="Server=localhost,1433;Database=LoanDb;User Id=sa;Password=LoanDev!Passw0rd;TrustServerCertificate=True" \
    LoanReadDb="Server=localhost,1433;Database=LoanReadDb;User Id=sa;Password=LoanDev!Passw0rd;TrustServerCertificate=True" \
    Mongo="mongodb://root:LoanDevMongo1@localhost:27017" \
    RabbitMq="amqp://guest:guest@localhost:5672" \
    Redis="localhost:6379" \
    JwtSigningKey="loan-dev-jwt-signing-key-change-me-0123456789abcdef" \
    FieldEncryptionKey="loan-dev-field-encryption-key-change-me" \
    DevSeedUserPassword="Dev!Passw0rd" \
    DevOAuthClientSecret="dev-oauth-client-secret-change-me" \
  || vault kv put secret/loan-api \
    LoanDb="Server=localhost,1433;Database=LoanDb;User Id=sa;Password=LoanDev!Passw0rd;TrustServerCertificate=True" \
    LoanReadDb="Server=localhost,1433;Database=LoanReadDb;User Id=sa;Password=LoanDev!Passw0rd;TrustServerCertificate=True" \
    Mongo="mongodb://root:LoanDevMongo1@localhost:27017" \
    RabbitMq="amqp://guest:guest@localhost:5672" \
    Redis="localhost:6379" \
    JwtSigningKey="loan-dev-jwt-signing-key-change-me-0123456789abcdef" \
    FieldEncryptionKey="loan-dev-field-encryption-key-change-me" \
    DevSeedUserPassword="Dev!Passw0rd" \
    DevOAuthClientSecret="dev-oauth-client-secret-change-me"'

# Stripe keys — restored from the gitignored local file if present so they
# survive dev Vault restarts. A missing file means "skip", not an error. The
# base block above already (re)created the document, so a plain patch is enough.
SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
SECRETS_FILE="$SCRIPT_DIR/vault-secrets.local.env"
if [ -f "$SECRETS_FILE" ]; then
  # shellcheck source=/dev/null
  . "$SECRETS_FILE"
  if [ -n "$StripeSecretKey" ] && [ -n "$StripeWebhookSecret" ]; then
    # Values are passed via -e (host env -> container env) and referenced inside
    # the single-quoted script, so the host shell never expands them into argv.
    docker exec \
      -e VAULT_ADDR=http://127.0.0.1:8200 \
      -e VAULT_TOKEN="$VAULT_TOKEN_VALUE" \
      -e StripeSecretKey="$StripeSecretKey" \
      -e StripeWebhookSecret="$StripeWebhookSecret" \
      loan-vault \
      sh -c 'vault kv patch secret/loan-api \
        StripeSecretKey="$StripeSecretKey" \
        StripeWebhookSecret="$StripeWebhookSecret"'
    echo "Restored Stripe keys from $SECRETS_FILE"
  else
    echo "WARN: $SECRETS_FILE exists but StripeSecretKey/StripeWebhookSecret are unset; skipped Stripe keys."
  fi
else
  echo "No $SECRETS_FILE (see vault-secrets.local.env.example); skipped Stripe keys."
fi
