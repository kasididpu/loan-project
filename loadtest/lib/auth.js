import http from 'k6/http';
import { check } from 'k6';
import { BASE_URL, CREDENTIALS } from './config.js';

// Logs in once (call from setup()) and returns a bearer token, so protected
// scenarios spend their whole run measuring the endpoint under test — not the
// login. The admin account is non-MFA, so one round-trip yields a full token.
export function login() {
  const res = http.post(`${BASE_URL}/auth/login`, JSON.stringify(CREDENTIALS), {
    headers: { 'Content-Type': 'application/json' },
  });
  check(res, { 'login succeeded': (r) => r.status === 200 });
  return res.json('access_token');
}

export function authJsonHeaders(token) {
  return {
    headers: {
      Authorization: `Bearer ${token}`,
      'Content-Type': 'application/json',
    },
  };
}

export function authHeaders(token) {
  return { headers: { Authorization: `Bearer ${token}` } };
}
