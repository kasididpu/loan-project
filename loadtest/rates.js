import http from 'k6/http';
import { check } from 'k6';
import { BASE_URL, rampProfile } from './lib/config.js';

// Cache path: the rate lookup sits behind a Redis cache-aside decorator. After
// the first call per (rateType, term) every hit within the TTL is served from
// Redis, so this scenario measures cache-read throughput (network + Redis),
// contrasted with the pure-CPU amortization baseline.
export const options = {
  ...rampProfile,
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<150', 'p(99)<300'],
  },
};

export default function () {
  const res = http.get(`${BASE_URL}/rates/Effective/12`);
  check(res, { 'status 200': (r) => r.status === 200 });
}
