# Bounded local synthetic worker

`local_synthetic.py` is a one-pass development worker: claim one metered accepted turn,
write a clearly labeled fixed synthetic reply with zero provider units, then exit.
There are no AI calls, paid requests or automatic background polling.

Use `python tools/check-database.py --api` from the repository root for a complete
self-contained demonstration. It provisions only a scratch PostgreSQL cluster, migrations
001-006 and a non-owner worker login, tests concurrency/expiry and invokes this process.
The harness supplies temporary credentials and stops the cluster afterward.

For an already configured **local synthetic** database, the process requires APP_ENV=local,
SYNTHETIC_ACCOUNTS_ONLY=true, COMPANION_SYNTHETIC_OWNER (fixture UUID), PGHOST=127.0.0.1,
PGPORT/PGDATABASE/PGUSER and securely inherited PGPASSWORD (or a PostgreSQL password file).
PG_BIN can select installed PostgreSQL binaries. Never put credential values in chat,
source files, command-line arguments or documentation. The login must be a member of
companion_worker and must not be a table owner, superuser or RLS-bypass role.

Each claim lasts 60 seconds in this adapter; database functions accept bounded 1-300 second
leases. Renew only a still-valid token. An expired claim can be replaced with a new token;
the old token then cannot finish. Completed receipts may retry after expiry with the
winning token and identical terminal payload/units. A failed completion leaves the turn
and hold intact for recovery. Turn state remains accepted while a lease owns the work.

This is not a production provider worker. Do not reuse synthetic expiry/reclaim behavior
for paid generation until uncertain provider outcomes, usage reconciliation, cancellation,
worker authentication and dispatch policy are implemented. Earlier trusted internal SQL
primitives remain available to the worker role; fencing protects these new cooperative
worker entry points, not arbitrary direct database writes by trusted server credentials.
