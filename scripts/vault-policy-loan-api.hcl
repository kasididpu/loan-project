# Least-privilege Vault policy for the Loan API (phase 8): read-only access to
# its own secret document and nothing else. In production the app authenticates
# via AppRole and receives a token bound to this policy — never the root token
# used in local dev. KV v2 splits data and metadata into separate paths, so both
# are granted read.
path "secret/data/loan-api" {
  capabilities = ["read"]
}

path "secret/metadata/loan-api" {
  capabilities = ["read"]
}
