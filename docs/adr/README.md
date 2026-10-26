# Architecture Decision Records

Each record captures one decision: the context that forced it, the options considered and the consequences accepted. Format: lightweight [MADR](https://adr.github.io/madr/).

| ADR | Decision | Status |
|---|---|---|
| [0001](0001-exchange-rates-current-state-plus-history.md) | Persist exchange rates as current state plus an append-only history | Accepted |
| [0002](0002-shared-kernel-and-conversion-contract.md) | Shared Kernel for money concepts and a published conversion contract between contexts | Accepted |
| [0003](0003-use-case-handlers-without-mediator.md) | One handler per use case, without a mediator library | Accepted |
| [0004](0004-ef-core-migrations.md) | EF Core Migrations replace `EnsureCreated` | Accepted |
| [0005](0005-sync-schedule-bootstrap-and-retries.md) | Sync schedule (Monday 06:00 UTC), startup bootstrap and retry layering | Accepted |
| [0006](0006-error-contract.md) | `Result` for expected failures, RFC 9457 ProblemDetails over HTTP | Accepted |
| [0007](0007-open-exchange-rates-acl-and-secrets.md) | Open Exchange Rates behind an anti-corruption layer; App ID kept out of source and URLs | Accepted |
| [0008](0008-monetary-precision-and-rounding.md) | `decimal` end to end, single half-away-from-zero rounding to 2 decimals | Accepted |
