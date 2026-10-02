# Store, IAP, subscriptions and RevenueCat

## BILL-01 — product model and setup gate

Use Apple/Google in-app purchase mechanisms for approved digital products with RevenueCat behind `IBillingClient` and `IEntitlementService`. Exact regional/store exceptions require current policy/legal review; do not silently enable external payment links. Q-005/Q-009/Q-012 gate product IDs, localized pricing, tax/account agreements, free quotas, trial/cancellation/refund wording and transfer policy.

Proposed initial product types: non-consumable cosmetics and subscriptions granting bounded voice/text usage plus explicitly listed cosmetics/features. No consumable currency at launch unless approved; if added, require a durable double-entry balance ledger and separate consume/grant semantics. Never advertise unlimited access against capped provider costs. Show store-fetched localized price and billing interval, trial conversion terms, renewal disclosure, restore and manage subscription links. Subscription cancellation normally changes future renewal; access duration comes from verified provider state, not client assumptions.

## BILL-02 — authoritative entitlement flow

Use a stable backend account ID as billing customer identity after authentication; never use email or device ID. Client initiates SDK purchase and may show pending/success feedback, then calls purchase sync. Backend verifies customer/store/environment and derives entitlement snapshot from trusted provider state. A client “purchase success” callback alone cannot grant inventory. Webhook processing converges independently if the app is killed. Offline purchases stay pending until verified.

Webhook: authenticate according to pinned RevenueCat integration (configured authorization and signature verification where supported/enabled), size limit, durable insert/dedupe by provider+environment+event_id, acknowledge only after durable acceptance, process asynchronously. The [RevenueCat webhook documentation](https://www.revenuecat.com/docs/integrations/webhooks) describes authenticated delivery and duplicate handling. Never rely on receiving every event or on event arrival order. Reconcile trusted customer state and transaction history; delayed cancellation must not override a newer valid renewal.

Grant/update inventory and entitlement version atomically with processed-event state. Duplicate sync/webhook yields one grant. Scheduled reconciliation detects missed renewals/refunds/expirations. Separate sandbox and production identities/products/events; sandbox can never grant production rights. Log only identifiers needed for support under approved retention.

## BILL-03 — lifecycle and edge cases

Handle pending purchase, user cancellation, billing retry, grace period, expiration, pause where supported, renewal, upgrade/downgrade, refund, revocation, restore, reinstall, account switch and store-account mismatch. Use normalized state `pending,active,grace,expired,revoked` plus effective expiry and provider snapshot time. Service access during grace follows approved policy. A subscription ending does not erase separately bought cosmetics.

RevenueCat aliases/transfers must obey explicit account-ownership policy; prevent a restore from stealing another account's grants. Account deletion explains that deleting app data may not cancel a store subscription and offers the correct management route. Financial records retained under approved policy must be minimized and separated from deleted chat identity. Entitlement caches have a bounded offline validity; costly AI always requires online server authorization.

Acceptance: Apple/Google sandbox tests cover purchase, renewal, cancel, expiration, grace, refund, restore and account switch; webhook duplicate/reorder/drop and app-killed-after-payment cases converge to correct state; no duplicate inventory/credit; display prices match stores; server enforces quota independently of UI; production configuration rejects sandbox credentials/products/events.
