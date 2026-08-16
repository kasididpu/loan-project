import http from 'k6/http';
import { check } from 'k6';
import { BASE_URL, SEED_CUSTOMER_ID, rampProfile } from './lib/config.js';
import { login, authJsonHeaders } from './lib/auth.js';

// Command path (CQRS write side): every request originates a new loan, which
// appends events to the event store (write DB). Compare its throughput and
// latency against query-path.js to see what the read/write split buys.
export const options = {
  ...rampProfile,
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<400', 'p(99)<800'],
  },
};

export function setup() {
  return { token: login() };
}

const body = JSON.stringify({
  customerId: SEED_CUSTOMER_ID,
  principal: 50000,
  rateType: 'Effective',
  termMonths: 12,
});

export default function (data) {
  const res = http.post(`${BASE_URL}/loans`, body, authJsonHeaders(data.token));
  check(res, { 'originated (2xx)': (r) => r.status >= 200 && r.status < 300 });
}
