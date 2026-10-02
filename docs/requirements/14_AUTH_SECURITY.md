# Authentication and security

## AUTH-01 — identity and sessions

Use an approved OIDC identity provider and platform-compatible sign-in flow with PKCE/state/nonce where applicable. Validate issuer, audience, signature, expiry and nonce server-side; never treat email alone as stable identity or proof of ownership. Support approved sign-in methods and required platform equivalents after store-policy review. Link accounts only after fresh proof of both identities; no automatic email-based merge. Decide anonymous-to-registered migration and purchase aliasing before enabling guest purchases.

Store refresh credentials in iOS Keychain/Android Keystore-backed storage; use short-lived access tokens, rotation/revocation where provider supports it and secure logout across devices. Access tokens never live in Unity PlayerPrefs or logs. Recent authentication is required for destructive privacy actions, identity linking and security changes. Backend rejects revoked/deleting accounts even if a JWT has not expired. Device clock is not authoritative for entitlements or auth.

## SEC-01 — threat model and controls

Threat actors: malicious mobile client, stolen token, prompt-injection attacker, abusive user, compromised dependency/vendor, overprivileged admin and billing replay attacker. Protect against IDOR, credential theft, mass account abuse, quota evasion, forged webhooks, unsafe media processing and data exfiltration. Enforce object-level authorization on every read/write/stream/room join. Validate lengths/schema/ranges and parameterize SQL; no user-controlled fetch URLs without strict SSRF protections. Bound uploads and decompress limits if upload functionality is later added.

Use TLS, encrypted storage/backups, secrets manager, least-privilege service identities, production/development isolation and private database networking. Rotate credentials through a rehearsed process. Mobile configuration is public; obfuscation cannot secure a server key. App/device attestation is an optional abuse signal, never sole authentication. Pinning is optional only with a tested rotation/recovery plan. Signed content/config improves integrity; no remote code execution in content updates.

Admin requires MFA/SSO, short sessions, RBAC and audited privileged operations. Restrict CORS, enforce CSRF protection for cookie-authenticated admin, secure cookie flags and CSP. No public admin registration. Break-glass access is time-limited, justified and audited. Export links, signed CDN URLs and session grants expire and are redacted from logs.

## SEC-02 — release evidence

Maintain data-flow threat model, dependency inventory/SBOM, secret scanning, SAST, dependency/license checks and targeted penetration testing before public launch. Fix all critical/high exploitable findings; accepted lower risks need owner, deadline and mitigation. Test authorization boundaries, account takeover paths, webhook forgery, unsafe deep links, replay, race conditions and rate limiting. Provider tool execution never bypasses these controls.

Acceptance: a forged user/room/item ID never yields another account's data or privileges; refresh replay/revocation and logout work; revoked account cannot mint voice credentials; APK/IPA/config scan finds no server credentials; unauthorized admin writes and unauthenticated webhooks fail without effects; security review findings have explicit disposition.
