# ADR-0002: Shared Kernel for money concepts and a published conversion contract

- **Status:** Accepted
- **Date:** 2026-10-26

## Context

Task 3 makes the Inventory context (products) depend on data owned by the new Exchange Rates context. The integration style decides how much each context knows about the other. It also decides where the conversion arithmetic lives, the part most likely to hide bugs (rate direction, rounding).

## Decision

- A minimal **Shared Kernel** in `Demo.Domain/Common` holds the concepts both contexts genuinely share: `Currency`, `Money` and `CurrencyConversion` (`From`, `To`, `Rate`, `AsOf`, `Convert(Money)`).
- The Exchange Rates context **publishes a read contract**, `ICurrencyConversionReader.GetConversionAsync(from, to)`, returning `Result<CurrencyConversion>`.
  - It returns `Identity` when `from == to`.
  - It returns `Unsupported` for currencies outside the configured set.
  - It returns `Unavailable` when no rate is stored.
- Inventory's catalog handler depends only on that contract and the shared value objects. It never sees `ExchangeRate`, its repository or its tables.
- The conversion arithmetic is a pure function on a value object, unit-testable without I/O.

## Alternatives considered

| Option | Why not chosen |
|---|---|
| **Catalog uses `IExchangeRateRepository` directly** | Couples Inventory to Exchange Rates' persistence model and aggregate. Any change there ripples into the catalog |
| **Full anti-corruption layer between the two contexts** (Inventory defines its own port plus an adapter) | Maximum isolation, but a third translation layer in a single-process monolith where both contexts already share the money vocabulary. Ceremony without benefit |
| **Conversion inside `Product`** | Product would need to know about rates. Conversion is a money concern, not a product concern |

## Consequences

- ➕ Clear dependency direction: Inventory → contract ← Exchange Rates.
- ➕ Rounding and direction rules live in one place with focused tests.
- ➕ Splitting the contexts into services later only changes the contract's implementation.
- ➖ The Shared Kernel is jointly owned, so changes to it affect both contexts. It is kept deliberately small for this reason.
