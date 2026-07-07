# ADR 0001: Unit of Work vs Repository + DbContext

## Status

Accepted

## Context

Endgame Phase 1 requires evaluating whether a formal Unit of Work (UoW) abstraction is beneficial for OmniOps, which already uses:

- Per-aggregate **repositories** (`ITelemetryRepository`, `IFleetRepository`, …)
- A single **scoped `AppDbContext`** per HTTP request / MediatR pipeline
- **`SaveChangesAsync`** on repositories for explicit persistence
- **Transactional outbox** via `OutboxSaveChangesInterceptor` on the same DbContext

## Decision

**Do not introduce a separate Unit of Work type.** The combination of scoped `AppDbContext`, composable MediatR `TransactionBehaviour` for `ITransactionalRequest`, and repository `SaveChangesAsync` is sufficient.

## Rationale

1. **Single DbContext per scope** already coordinates change tracking across all repositories in one request.
2. **Outbox interceptor** must run in the same `SaveChanges` call as aggregate persistence; a second UoW layer would duplicate that responsibility.
3. **MediatR transaction pipeline** covers multi-step command atomicity without wrapping every handler in boilerplate.
4. Adding `IUnitOfWork` would mostly re-export `SaveChangesAsync` and increase indirection without new capability.

## Consequences

- Commands that need atomicity implement `ITransactionalRequest` and rely on `TransactionManager`.
- Repositories remain thin EF wrappers; handlers call `SaveChangesAsync` when persistence should commit.
- If cross-service sagas are introduced later, revisit with explicit process managers—not a generic UoW.
