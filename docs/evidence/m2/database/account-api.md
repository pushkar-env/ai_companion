# Account API integration — 2026-10-04

Passed: .NET 10.0.301 builds with zero warnings/errors, locked Npgsql 10.0.3 restore,
fresh M0 HTTP smoke checks, and `python tools/check-database.py --api` with **34 SQL
groups + 23 actual HTTP checks**. Combined output is in checks.txt. Earlier host execution
block did not reproduce; no security policy was changed.

Tests use PostgreSQL 18.1 scratch cluster, a real non-owner login with no superuser/RLS
bypass, random expiring synthetic-account tokens, and a loopback-only ASP.NET process.
Evidence includes missing/bad/expired identity, browser Origin/foreign Host rejection,
body/header owner-spoof rejection, cross-owner admission/read denial, pooled connection
isolation across five alternations, bounded input, matching/conflicting retry behavior,
one durable reservation per turn, account/global caps, quota rollback and API restart.
Production startup is rejected. Temporary credential fixture is removed and processes
are stopped after tests. No real user data, provider calls or cloud resources used.

Migration 004 adds owner-matched turn/reservation binding and atomic metered admission.
The development /local/v1 response is not the production OpenAPI contract. Accepted
turns are not yet processed by an AI worker. Production identity, guest transfer, complete
rate limiting, terminal settlement integration, SSE/history, privacy and release checks
remain pending. Unity scene, portrait settings and Editor layout were untouched.
