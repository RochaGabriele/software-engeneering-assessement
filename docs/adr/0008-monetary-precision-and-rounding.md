# ADR-0008: `decimal` end to end; single half-away-from-zero rounding to 2 decimals

- **Status:** Accepted
- **Date:** 2026-10-26

## Context

Currency conversion is where small mistakes become visible money errors. Several traps are specific to this stack:

- **Direction.** OXR quotes "1 USD = *r* EUR". Dividing instead of multiplying inverts every price.
- **Binary floating point.** Deserializing JSON numbers into `double` introduces representation error before any arithmetic happens.
- **Default rounding.** `Math.Round` uses banker's rounding (`MidpointRounding.ToEven`) by default, so `0.125` becomes `0.12`. Consumer price display expects `0.13`.
- **Precision.** The baseline maps `Product.Price` as `decimal(18,2)`. Reusing that for rates would truncate `0.921234` to `0.92` as soon as the database is not SQLite (SQLite ignores the declaration and stores TEXT).
- **Scale.** SQLite returns `103.30` as `103.3`, so JSON output alternates between `103.3` and `95.04`, while the task's example shows `95.50`.

## Decision

- `decimal` everywhere: JSON deserialization, domain, persistence.
- Rate column precision is declared explicitly as `decimal(18,8)`. Product prices keep `decimal(18,2)`.
- Conversion in `CurrencyConversion.Convert`:
  - `converted = Math.Round(amount × rate, 2, MidpointRounding.AwayFromZero)`;
  - the full-precision rate is used and rounding happens **once**, at the end;
  - the result is normalized to scale 2.
- Identity conversion (USD) also normalizes the scale, so every price serializes with exactly 2 decimals.
- Unit tests pin the direction (`100 USD × 0.92 = 92.00 EUR`), the midpoint case and the scale.

## Alternatives considered

| Option | Why not chosen |
|---|---|
| Banker's rounding | Statistically unbiased for aggregates, but surprising for an individual displayed price |
| Round the rate first, then the price | Two rounding steps compound error |
| Format to 2 decimals in a JSON converter | Moves a money rule into serialization. The domain value would still carry an arbitrary scale |

## Consequences

- ➕ Deterministic, explainable prices with one rule in one place.
- ➖ The 2-decimal rule assumes 2 minor units, which is true for all supported currencies (USD, EUR, CAD, GBP, CHF). Supporting JPY (0) or KWD (3) requires minor-unit metadata on `Currency`.
