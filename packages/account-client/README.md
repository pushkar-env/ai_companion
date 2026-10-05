# Synthetic account/history client

Local .NET 10 client foundation for the account API, with no additional packages.
Not a Unity-compatible assembly yet; the Editor still uses its session-only chat.

Set APP_ENV=local and SYNTHETIC_ACCOUNTS_ONLY=true. Construct SyntheticClient with a
loopback http://127.0.0.1:port/ URI, a short-lived synthetic bearer and conversation UUID.
Keep the bearer in memory; never use URLs, logs or source files for it. The client does
not read database credentials. Dispose it only after awaited operations finish.

Call Load() to hydrate admission/terminal events from paginated durable history. Then
Follow(callback, cancellationToken) resumes from the last applied cursor. The callback
is a notification after application: a callback failure does not roll back the projection.
History.Turns is a snapshot of user text, optional assistant text, state and version.
Use one instance per account/conversation; discard it on logout or account switch.
Load/Follow are serialized. Read the projection after an operation or in its callback;
this development client does not expose a concurrent UI dispatcher.

EOF reconnects from the applied cursor, never resubmits a prompt. Partial frames are
discarded, duplicate events ignored, sequence gaps rejected. Connection/read deadline is
45 seconds. Up to three reconnects use 1/2/4-second delays plus 0..249ms jitter, then fail.
401/403/404/invalid cursor and other non-transient HTTP responses fail for caller action.
429/503/network failures retry within that bound. Caller cancellation stops reconnects.
State reports idle/loading/ready/connecting/connected/reconnecting/stopped/error.

History and cursor are session-only; maximum 1000 turns, SSE frame maximum 131072 chars.
No disk cache, login implementation, prompt submission UI or provider dispatch is added.
These are local limits, not approved product retention/free-trial policies.

Checks:

```powershell
dotnet run --project tests/account-client/Companion.AccountClient.Checks.csproj
python tools/check-database.py --api
```

The first runs parser/projection/socket recovery checks. The second additionally builds
and executes the client against its disposable API/database with an ignored synthetic
credential fixture. It checks history hydration, repeat load, cross-owner rejection and
SSE projection. No Unity assets or Editor state are modified.
