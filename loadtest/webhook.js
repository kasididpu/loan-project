import http from 'k6/http';
import crypto from 'k6/crypto';
import { check, fail } from 'k6';
import { BASE_URL, rampProfile } from './lib/config.js';

// Payment webhook path. The endpoint's fixed per-request cost is the security
// boundary: it fetches the signing secret from Vault and HMAC-verifies the raw
// body on EVERY call. This scenario signs benign events (a type the handler
// acknowledges without touching the database), so it measures that verify +
// secret-fetch overhead cleanly, with no payment records written and no errors.
//
// Requires the same secret the app reads from Vault (StripeWebhookSecret):
//   k6 run -e WEBHOOK_SECRET=$StripeWebhookSecret loadtest/webhook.js
// Skip this scenario if Stripe test keys are not configured locally.
const SECRET = __ENV.WEBHOOK_SECRET;

export const options = {
  ...rampProfile,
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<300', 'p(99)<600'],
  },
};

export function setup() {
  if (!SECRET) {
    fail('WEBHOOK_SECRET is required (matches Vault StripeWebhookSecret). ' +
      'Run: k6 run -e WEBHOOK_SECRET=$StripeWebhookSecret loadtest/webhook.js');
  }
}

export default function () {
  const ts = Math.floor(Date.now() / 1000);
  // A well-formed but unhandled event type: the handler verifies the signature,
  // then returns 200 without any business processing.
  const payload = JSON.stringify({
    id: `evt_load_${__VU}_${__ITER}`,
    object: 'event',
    api_version: '2024-06-20',
    created: ts,
    type: 'payment_intent.created',
    data: { object: { id: 'pi_load', object: 'payment_intent' } },
  });

  // Stripe's scheme: HMAC-SHA256 over "<timestamp>.<raw body>".
  const signature = crypto.hmac('sha256', SECRET, `${ts}.${payload}`, 'hex');

  const res = http.post(`${BASE_URL}/webhooks/stripe`, payload, {
    headers: {
      'Content-Type': 'application/json',
      'Stripe-Signature': `t=${ts},v1=${signature}`,
    },
  });
  check(res, { 'status 200': (r) => r.status === 200 });
}
