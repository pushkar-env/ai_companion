# Environment variables, configuration and secrets

## ENV-01 — template

Generate a repository `.env.example` from this template. Empty values mean “not configured”, not valid credentials. Local bootstrap may generate disposable local-only credentials into gitignored files. Startup validates by environment/capability and reports missing variable names without revealing values. No production secret belongs in Unity, admin browser bundles, Git, chat or logs.

```dotenv
# Local mock mode only; production rejects mock flags and localhost.
APP_ENV=local
MOCK_EXTERNAL_SERVICES=true
API_PUBLIC_BASE_URL=http://localhost:8080
ADMIN_PUBLIC_BASE_URL=http://localhost:3000
DATABASE_URL=
REDIS_URL=
OIDC_ISSUER=
OIDC_AUDIENCE=
OIDC_CLIENT_ID=
OIDC_SERVER_CLIENT_SECRET=
SECRETS_PROVIDER=local
KMS_KEY_ID=
LIVEKIT_URL=
LIVEKIT_API_KEY=
LIVEKIT_API_SECRET=
AI_TEXT_PROVIDER=mock
AI_TEXT_MODEL=
AI_REALTIME_PROVIDER=mock
AI_REALTIME_MODEL=
AI_PROVIDER_API_KEY=
AI_PROVIDER_REGION=
STT_PROVIDER=mock
STT_API_KEY=
TTS_PROVIDER=mock
TTS_API_KEY=
EMBEDDING_PROVIDER=mock
EMBEDDING_MODEL=
EMBEDDING_DIMENSIONS=
EMBEDDING_API_KEY=
MODERATION_PROVIDER=mock
MODERATION_API_KEY=
REVENUECAT_SERVER_API_KEY=
REVENUECAT_WEBHOOK_AUTH_SECRET=
REVENUECAT_WEBHOOK_SIGNING_SECRET=
REVENUECAT_PUBLIC_IOS_KEY=
REVENUECAT_PUBLIC_ANDROID_KEY=
ASSET_BUCKET=
ASSET_CDN_BASE_URL=
ASSET_MANIFEST_SIGNING_KEY_REF=
FCM_PROJECT_ID=
FCM_SERVICE_ACCOUNT_SECRET_REF=
APNS_TEAM_ID=
APNS_KEY_ID=
APNS_PRIVATE_KEY_SECRET_REF=
APNS_BUNDLE_ID=
OTEL_EXPORTER_OTLP_ENDPOINT=
OTEL_EXPORTER_OTLP_HEADERS=
CRASH_REPORTING_PUBLIC_DSN=
FEATURE_CONFIG_SOURCE=local
DEFAULT_LOCALE=en
DATA_REGION=
POLICY_VERSION=
CONSENT_VERSION=
MONTHLY_SPEND_CAP_MINOR=
SPEND_CURRENCY=
VOICE_MAX_SESSION_SECONDS=
FREE_TEXT_DAILY_LIMIT=
FREE_VOICE_DAILY_SECONDS=
TEXT_RETENTION_DAYS=
MEMORY_RETENTION_DAYS=
LOG_RETENTION_DAYS=
BACKUP_RETENTION_DAYS=
```

## ENV-02 — ownership and exposure

| Group | Scope / sensitive values | Who sets and validates |
|---|---|---|
| Database/Redis | Server only; connection URLs contain credentials | Operator; connection and TLS checks |
| OIDC | Issuer/audience/client ID public; server secret private and only if required | Identity owner; issuer/JWKS/login test |
| LiveKit | URL public, key/secret server only | Voice operator; scoped token and native device join |
| AI/STT/TTS/embedding/moderation | Server keys private; model/region config not client authority | Approved provider owner; capability and quota test |
| RevenueCat | Platform public SDK keys client-safe; server/webhook secrets private | Commerce owner; sandbox sync/webhook test |
| Assets | CDN URL public; publishing/signing credentials secret-manager only | Content operator; integrity and cache test |
| Push | IDs are identifiers; signing/service-account credentials server only | Store owner; device delivery test |
| Telemetry | DSN may be public by design; exporter auth headers private | Operator; redaction fixture |
| Business/retention | Non-secret but approval-controlled | Product/legal; production startup blocks missing approvals |

Do not overload one credential across providers/environments. If the selected platform supports workload identity, prefer it to long-lived static keys and document adapter-specific variable names. Provider-specific auth details may require additional variables, declared in typed config schemas and this template. Public client configuration is built from an explicit allowlist, never by copying server environment files.

## ENV-03 — setup checklist

Create accounts/projects only after owner approval. Record environment, region, secret reference, scope, rotation owner and last validated date, not values. Store signing certificates, provisioning profiles, Android upload keys and CI credentials in protected platform stores with recovery procedure. Validate Apple bundle ID/Android application ID, deep-link domains, push credentials, product IDs and RevenueCat environment mapping together. Production CI must require explicit environment approval and reject missing/unapproved caps/policies.

Acceptance: sample file contains no working secrets; missing configuration fails fast with redacted actionable errors; production cannot start with mock identity/commerce or empty spending/retention policy; a mobile build contains only allowlisted public config; credential rotation is rehearsed without writing values into logs.
