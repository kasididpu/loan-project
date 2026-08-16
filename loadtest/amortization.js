import http from 'k6/http';
import { check } from 'k6';
import { BASE_URL, rampProfile } from './lib/config.js';

// CPU-bound baseline: the amortization preview is pure computation (no auth, no
// database, no cache), so this scenario measures the money-calc hot path in
// isolation — the fastest endpoint and the ceiling other paths are compared to.
export const options = {
  ...rampProfile,
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<150', 'p(99)<300'],
  },
};

const body = JSON.stringify({
  principal: 100000,
  annualRate: 0.12,
  termMonths: 12,
  rateType: 'Effective',
});

export default function () {
  const res = http.post(`${BASE_URL}/amortization/preview`, body, {
    headers: { 'Content-Type': 'application/json' },
  });
  check(res, {
    'status 200': (r) => r.status === 200,
    'schedule closes at zero': (r) => r.json('schedule')[11].remainingBalance === 0,
  });
}
