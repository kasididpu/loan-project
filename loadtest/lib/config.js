// Shared config for the k6 load tests (phase 10). Everything is overridable via
// `-e KEY=value` so the same scripts run against the single dev instance
// (default) or the HA stack behind nginx (`-e BASE_URL=http://localhost:8080`).

export const BASE_URL = __ENV.BASE_URL || 'http://localhost:5213';

// Non-MFA seed account (Admin role satisfies every policy) — the same non-secret
// dev password the seeders use. Never put a real secret here.
export const CREDENTIALS = {
  username: __ENV.LOAN_USER || 'admin',
  password: __ENV.LOAN_PASSWORD || 'Dev!Passw0rd',
};

// Seeded, KYC-verified customer (Somsri) — safe to originate loans against.
export const SEED_CUSTOMER_ID =
  __ENV.SEED_CUSTOMER_ID || '5eed0000-0000-0000-0000-000000000002';

// A modest, reproducible ramp shared by every scenario: warm up, hold, ramp
// down. Tune VUs with `-e VUS=50`. Thresholds make a regression fail the run.
const peak = Number(__ENV.VUS || 20);
export const rampProfile = {
  stages: [
    { duration: '15s', target: peak },
    { duration: '30s', target: peak },
    { duration: '10s', target: 0 },
  ],
};
