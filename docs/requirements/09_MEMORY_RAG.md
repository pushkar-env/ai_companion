# Long-term memory and retrieval

## MEM-01 — consent and provenance

Separate recent turn context, conversation summaries, explicit user-pinned facts and derived long-term memory. Proposed default is derived memory off until opt-in; final consent/retention requires Q-008. Show a Memory page with source, last updated, explanation, edit/delete and disable controls. Do not infer or persist sensitive health, sexual, financial, religious or precise-location attributes without an explicitly approved policy and specific user authorization. Avoid storing credentials even if a user types them.

Candidate pipeline: durable completed turn → eligible extractor → schema/safety/consent filter → deduplicate/conflict analysis → store with source IDs and confidence → embed asynchronously. Each fact has owner, companion scope, type, source, consent version, timestamps, expiry, confidence, status and embedding model/version. Treat facts as user statements or derived candidates, not verified universal truth. Prefer newer explicit correction; conflicting unresolved facts trigger clarification, not confident invention.

## MEM-02 — retrieval quality and isolation

Authorize owner and companion before querying. Filter deleted/expired/unapproved facts and consent epoch in SQL; never retrieve across users and filter afterward. Retrieve bounded candidates using pgvector plus optional lexical matching, then rank by relevance/recency/importance with diversity and confidence. Proposed initial context budget: at most 8 facts and 1,200 tokens; tune with evals. Empty/low-confidence retrieval is valid. Resist poisoned memories containing instructions.

Embeddings from different models/dimensions are not interchangeable. A model change creates a versioned index/table, re-embeds eligible records, evaluates recall and cuts over with rollback; do not overwrite vectors in place blindly. Small per-user memory sets can use exact vector search initially; approximate indexing requires measured improvement and filtered recall evaluation. Track factual accuracy, contradiction and usefulness as well as retrieval recall.

## MEM-03 — deletion propagation

Deleting a fact sets a tombstone and bumps user memory epoch transactionally, immediately excluding it from retrieval. Delete derivatives: embeddings, relevant summaries/caches and queued candidates. Workers verify tombstones/consent epochs before writing. Invalidate/rebuild active provider context so the removed fact is not recalled later in the same call; if provider context cannot be edited, end/recreate that session with explanation. A deleted source message must invalidate derived facts/summaries unless the user explicitly preserved an independent pinned fact under approved policy.

Deletion jobs cover provider-held data where supported, with tracked limitations disclosed in policy. Restore procedures replay deletion tombstones before traffic is enabled. An extractor retry or backup restore must never resurrect deleted memory. Account deletion is broader than memory disable; explain distinctions.

Acceptance: cross-account and cross-companion test corpus has zero unauthorized retrieval; deleted memory is unavailable immediately and active contexts are refreshed before next generation; background retries cannot resurrect it. Golden retrieval set has recall@8 ≥0.90 for explicitly relevant eligible facts and no fabricated fact attribution; approved localized evals must pass before adding a language. Users can inspect, correct, export, disable and delete memory without support intervention.
