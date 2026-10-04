# Durable quota primitives — 2026-10-04

`python tools/check-database.py`: **34 total groups passed** on PostgreSQL 18.1,
Windows, isolated synthetic cluster. Fresh combined results: `checks.txt`.

New evidence: idempotent reservation and settlement; account-cap enforcement; conflicting
retry/unfunded-usage rejection; unused-unit release; runtime ledger/cap restrictions;
cross-owner denial; rollback of accounting; invalid deadline rejection; failed admission
after reserving strands no hold; ten parallel requests across two accounts against one
global cap (five admitted, five denied); concurrent settlement charges once; crash recovery
retains holds/ledger and retry outcomes. All 23 preceding database groups also passed.

Test amounts are not product prices/free allowances. No public API/provider is metered
yet. Production throughput, headroom, usage increments, trusted provider reconciliation,
expiry worker and guest caps remain pending. Expiry never implies zero incurred cost.
No real data, cloud resource or paid service. Whitespace check passed; Unity assets,
portrait settings and Editor layout were untouched.
