# Least-privilege Vault policy for the Loan API (phase 8, extended in phase 9):
# read-only access to its own secret documents and nothing else. The app keeps
# one document per environment — "loan-api" for host dev (localhost connection
# strings) and "loan-docker" for the containerized HA stack (service-name ones).
# Both are granted; no other app's secrets are reachable. In production the app
# authenticates via AppRole and receives a token bound to this policy, never the
# root token used in local dev. KV v2 splits data and metadata into separate
# paths, so both are granted read for each document.
path "secret/data/loan-api" {
  capabilities = ["read"]
}

path "secret/metadata/loan-api" {
  capabilities = ["read"]
}

path "secret/data/loan-docker" {
  capabilities = ["read"]
}

path "secret/metadata/loan-docker" {
  capabilities = ["read"]
}
