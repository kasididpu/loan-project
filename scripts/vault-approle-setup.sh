#!/usr/bin/env sh
# Showcase (phase 8): provisions least-privilege access to the Loan API's secrets
# through Vault AppRole instead of the dev root token. Run after seed-vault-dev.sh.
#
# The dev Vault runs in dev mode, so this is illustrative — the app still uses the
# root token locally. It writes the loan-api policy, enables AppRole, and creates
# a role bound to that policy, then prints a role_id + secret_id pair. A real
# deployment injects those as VAULT_ROLE_ID / VAULT_SECRET_ID; the app would
# exchange them for a short-lived token. No real credential ever enters the repo.
set -e

VAULT_TOKEN_VALUE="${VAULT_DEV_ROOT_TOKEN_ID:-loan-dev-root}"
SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)

# Copy the policy file into the container, then load and wire up AppRole.
docker cp "$SCRIPT_DIR/vault-policy-loan-api.hcl" loan-vault:/tmp/loan-api.hcl
docker exec \
  -e VAULT_ADDR=http://127.0.0.1:8200 \
  -e VAULT_TOKEN="$VAULT_TOKEN_VALUE" \
  loan-vault \
  sh -c '
    vault policy write loan-api /tmp/loan-api.hcl
    vault auth enable approle 2>/dev/null || true
    vault write auth/approle/role/loan-api \
      token_policies=loan-api token_ttl=1h token_max_ttl=4h
    echo "role_id:"
    vault read -field=role_id auth/approle/role/loan-api/role-id
    echo ""
    echo "secret_id:"
    vault write -f -field=secret_id auth/approle/role/loan-api/secret-id
    echo ""
  '
