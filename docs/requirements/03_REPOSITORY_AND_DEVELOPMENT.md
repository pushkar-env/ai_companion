# Repository and development workflow

## REPO-01 — proposed monorepo

```text
AGENTS.md
docs/requirements/          # this package, including QUESTIONS.md
docs/STATUS.md
docs/DECISIONS.md
docs/runbooks/
apps/unity/Assets/Companion/
  Core/                    # domain, immutable models, ports
  Features/                # chat, call, avatar, wardrobe, settings
  Infrastructure/          # HTTP, platform storage, vendor adapters
  Presentation/            # views, state machines, animation
  Tests/                   # edit-mode and play-mode
apps/admin/                # typed frontend with accessible components
services/api/              # modular monolith
services/jobs/             # outbox and scheduled/retry work
services/voice-agent/      # realtime media/AI orchestration
packages/contracts/       # OpenAPI, JSON Schema, generated clients
packages/policies/        # versioned behavior/safety fixtures
assets/source/            # licensed DCC sources, Git LFS as needed
assets/manifests/         # rigs, wardrobe, import validation
tests/contract/
tests/e2e/
tests/load/
tests/evals/
infra/                    # reviewable IaC, no secrets/state in Git
tools/                    # bootstrap, lint, validation, fixtures
.env.example              # generated from 21_ENVIRONMENT.md
```

Unity assembly definitions enforce Core independent of vendor packages and Unity presentation where possible. Place editor-only tools in Editor assemblies. Keep asset GUIDs/meta files in version control; set visible metadata and text serialization. Large source assets use documented LFS rules. Generated clients are reproducible and CI checks drift. Never commit Library, Temp, build caches, signing artifacts or provider secrets.

## REPO-02 — reproducibility

Provide a one-command local bootstrap and one-command test entrypoint suitable for the chosen shell; document prerequisites and exact Unity Editor/package versions. Local compose includes Postgres with pgvector, Redis and object-store emulator; mock AI, voice, identity, commerce and push are deterministic. Use fake users only. Local no-network mode must not contact production.

Configurations: local/mock, development integration, staging/sandbox, production. Compile-time production guards reject mock identity/billing/providers, localhost URLs and missing required configuration. Mock mode is visually labeled. Development seed data is separate from migrations and impossible to run against production without explicit guarded operator procedure.

Acceptance: clean checkout bootstraps on documented developer platforms; Unity and API share matching generated contracts; lockfiles reproduce builds; secret scanning passes; sample data can be reset locally without affecting any remote environment; an ADR captures exact runtime choices and compatibility spike results.
