# Notifications and deep links

## NOTIFY-01 — respectful engagement

Use APNs/FCM behind an adapter, with native permission handling through Unity bridges. Ask permission after the user enables a useful notification category, not automatically at first launch. Categories: user-requested reminders, account/service notices and separately opt-in engagement. Respect per-category opt-out, timezone, quiet hours and approved frequency cap (proposed maximum one engagement push/day). Security/service notices must follow approved policy and avoid sensitive content.

Never include chat text, intimate memories, inferred distress, wardrobe spending or guilt-inducing companion messages in lock-screen notifications. Default copy is generic. No “I need you,” jealousy, streak punishment or deceptive urgency. A notification is not authorization for a call or microphone capture.

## NOTIFY-02 — reliable scheduling

Persist jobs with user, category, consent version, scheduled UTC time, originating timezone and dedupe key. Recheck consent/account status at send time, not just job creation. Handle daylight-saving changes, travel, quiet-hour deferral, revoked/rotated tokens, provider retries and invalid-token removal. Tokens belong to installation+account; logout/account deletion removes targeting and pending jobs. Expose clear “all devices” preferences.

Deep links carry only allowlisted route and opaque resource reference, validate auth/ownership after opening and recover to a safe screen if missing. Purchases/calls/destructive actions require normal UI confirmation; pushes cannot trigger them automatically. Keep attribution identifiers pseudonymous and retention-bounded.

Acceptance: no send after opt-out, logout or deletion, including a race with queued work; repeated job deliveries produce at most one intended send per dedupe key where provider semantics allow, with duplicates monitored; DST/quiet-hour fixtures pass; notification tap while signed out does not reveal private text; denied permission leaves in-app reminders usable.
