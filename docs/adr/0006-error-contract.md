# ADR-0006: `Result` for expected failures, RFC 9457 ProblemDetails over HTTP

- **Status:** Accepted
- **Date:** 2026-10-26

## Context

The baseline lets exceptions escape: an unknown SKU returns **500 with a stack trace** (`FirstAsync` throws `InvalidOperationException`). Task 3 adds expected failure cases that the task does not specify: a malformed or unsupported currency, and a supported currency whose rate has not been fetched yet. Clients need one consistent, machine-readable error format, and expected outcomes should not travel as exceptions.

## Decision

- **Application layer.** Handlers return `Result<T>`, which carries either a value or an `Error(Code, Message, Type)`. `ErrorType` is `Validation`, `NotFound` or `Unavailable`.
- **Exceptions** are reserved for programming errors and invariant violations (`DomainException`) and for infrastructure failures.
- **HTTP layer.**
  - `AddProblemDetails()` + `UseExceptionHandler()` + `UseStatusCodePages()` make the framework emit RFC 9457 `application/problem+json`. This covers unhandled exceptions (500 with `traceId`, no stack trace) and empty error responses such as unknown routes. No custom exception handler is needed.
  - Controllers map `ErrorType` → `Validation` 400, `NotFound` 404, `Unavailable` 503, and add the error's stable `code` as a ProblemDetails extension.

| Case | Status | Rationale |
|---|---|---|
| Malformed or unsupported `currency` | **400** | The client sent a value outside the documented set. `detail` lists the supported codes so the client can correct it |
| Supported currency, no rate stored yet | **503** | The request is valid. The server temporarily cannot fulfil it until the next sync (the startup bootstrap closes this window quickly) |
| Unknown SKU | **404** | The resource does not exist |

## Alternatives considered

| Option | Why not chosen |
|---|---|
| Exceptions for expected cases + exception filter | Uses exceptions for control flow, hides the failure modes from handler signatures, and costs more at runtime |
| 422 for "rate unavailable" | 422 means the request content is semantically invalid. Here the request is valid and the missing piece is server-side data |
| Fall back to USD prices when the rate is missing | Silently returns a currency the client did not ask for. Rejected even with the `currency` field set honestly, because it surprises the client |
| 404 for an unsupported currency | The catalog resource exists. The problem is the parameter value |

## Consequences

- ➕ Failure modes are visible in handler signatures and testable without HTTP.
- ➕ One error format across the API, documented in Swagger (`ProducesResponseType`).
- ➖ A small `Result`/`Error` type to maintain. This is preferred to a library dependency for ~40 lines of code.
