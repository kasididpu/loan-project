import http from 'k6/http';
import { check } from 'k6';
import { BASE_URL, rampProfile } from './lib/config.js';
import { login, authHeaders } from './lib/auth.js';

// Query path (CQRS read side): reads the portfolio summary straight from the
// Read DB (a projection kept in sync by events, never a cross-database query).
// Its numbers are the counterpart to command-path.js.
export const options = {
  ...rampProfile,
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<250', 'p(99)<500'],
  },
};

export function setup() {
  return { token: login() };
}

export default function (data) {
  const res = http.get(`${BASE_URL}/reports/portfolio-summary`, authHeaders(data.token));
  check(res, { 'status 200': (r) => r.status === 200 });
}
