# ADR-0003: One handler per use case, without a mediator library

- **Status:** Accepted
- **Date:** 2026-10-26

## Context

The baseline exposes the application layer through a facade per module (`IInventoryModule`). Tasks 2–3 add three use cases: synchronize rates, list the catalog and resolve a conversion. Growing the facade would mix unrelated operations and dependencies in one class (SRP) and force consumers to depend on methods they do not use (ISP).

## Decision

- Each use case is a query or command record plus one handler. The handlers implement `IQueryHandler<TQuery, TResult>` or `ICommandHandler<TCommand, TResult>`.
- Code is grouped by bounded context and use case, for example `Modules/Inventory/GetProductCatalog/`.
- Controllers and the Hangfire job depend on handler interfaces and are resolved by the DI container. There is no mediator in between.
- The baseline `IInventoryModule.GetProduct` becomes `GetProductBySku` under the same pattern, so the layer has a single style.

## Alternatives considered

| Option | Why not chosen |
|---|---|
| **Keep and extend module facades** | Consistent with the baseline, but the facade accumulates every operation and dependency of the context |
| **MediatR / Wolverine** | MediatR moved to a commercial license in 2025. Dispatch-by-type indirection makes "go to implementation" harder for three handlers that DI can resolve directly. Pipeline behaviors are not needed yet |

## Consequences

- ➕ Each handler has one reason to change and is tested in isolation with hand-written fakes.
- ➕ A new use case adds a folder and does not touch existing code (OCP).
- ➕ No third-party dependency or license concern.
- ➖ Cross-cutting behavior (logging, validation pipelines) would need decorators if it ever becomes necessary. This is deferred until there is a need.
