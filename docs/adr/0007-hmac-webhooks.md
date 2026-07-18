# 0007 — HMAC-SHA256 signed webhooks with capped retries and a delivery log

**Status:** Accepted

## Context

Subscribers need to verify that an order-lifecycle event genuinely came from Bazaar and was not
tampered with, and operators need visibility into delivery outcomes. A flaky subscriber endpoint
must never break the request that triggered the event.

## Decision

`WebhookDispatcher` fans each event (`order.created`/`paid`/`fulfilled`/`refunded`) out to every
active subscription that subscribes to it:

- Serialize a camelCase JSON payload `{ event, timestamp, data }`.
- **HMAC-SHA256 sign** it with the subscription's per-subscription secret (lowercase hex).
- Deliver via the `IWebhookSender` port with up to `MaxAttempts = 3` retries until success.
- Record **every** delivery attempt (`WebhookDelivery`: success, response status, attempt count).

## Consequences

- Delivery is **best-effort**: failures are retried up to the cap, logged, and never surfaced to the
  triggering caller.
- The default `FakeWebhookSender` is deterministic (URLs containing `fail` always error) so retry
  capping is testable; a production adapter POSTs the payload with the signature header.
- Delivery is at-least-once up to the retry cap; subscribers should treat handling as idempotent.
