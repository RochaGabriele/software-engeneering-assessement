# ADR-0008: `decimal` end to end; single half-away-from-zero rounding to the currency's minor units

- **Status:** Accepted (revised 2026-10-29 after code review: rounding follows each currency's ISO 4217 minor units instead of a fixed 2)
- **Date:** 2026-10-26

## Context

Currency conversion is where small mistakes become visible money errors. Several traps are specific to this stack:

- **Direction.** OXR quotes "1 USD = *r* EUR". Dividing instead of multiplying inverts every price.
- **Binary floating point.** Deserializing JSON numbers into `double` introduces representation error before any arithmetic happens.
- **Default rounding.** `Math.Round` uses banker's rounding (`MidpointRounding.ToEven`) by default, so `0.125` becomes `0.12`. Consumer price display expects `0.13`.
- **Storage type.** The baseline maps `Product.Price` with `HasColumnType("decimal(18,2)")`. In SQLite that type name gets NUMERIC affinity, so the engine converts the TEXT that EF writes into a binary floating-point REAL. On the baseline database `typeof(Price)` returns `'real'`: the `decimal` guarantee is lost at rest.
- **Precision.** Reusing `decimal(18,2)` for rates would also truncate `0.921234` to `0.92` on any engine that enforces it, such as SQL Server.
- **Scale.** SQLite returns `103.30` as `103.3`, so JSON output alternates between `103.3` and `95.04`, while the task's example shows `95.50`.
- **Minor units.** Not every currency has cents: JPY has 0 decimals and KWD has 3. The target list is configurable, so a fixed 2-decimal rule would silently produce fractional yen.

## Decision

- `decimal` everywhere: JSON deserialization, domain, persistence.
- Precision is declared with `HasPrecision`, never with `HasColumnType("decimal(...)")`: rates `(18, 8)`, product prices `(18, 2)`. EF then stores TEXT in SQLite, which is exact. A probe on EF Core 10.0.14 confirmed `typeof = 'text'` and a lossless round-trip of `0.92123456`.
- Conversion in `CurrencyConversion.Convert`:
  - `converted = Math.Round(amount × rate, target.MinorUnits, MidpointRounding.AwayFromZero)`;
  - the full-precision rate is used and rounding happens **once**, at the end;
  - the result is normalized to exactly the currency's minor units.
- `Currency.MinorUnits` follows ISO 4217: 2 by default, with the exceptions (0 for JPY, KRW, …; 3 for KWD, BHD, …; 4 for CLF, UYW) in one table.
- Identity conversion (USD) also normalizes the scale, so every price serializes with exactly the currency's decimals (2 for all required currencies).
- Unit tests pin the direction (`100 USD × 0.92 = 92.00 EUR`), the midpoint case, the scale, and 0- and 3-decimal currencies.

## Alternatives considered

| Option | Why not chosen |
|---|---|
| Banker's rounding | Statistically unbiased for aggregates, but surprising for an individual displayed price |
| Round the rate first, then the price | Two rounding steps compound error |
| Format to 2 decimals in a JSON converter | Moves a money rule into serialization. The domain value would still carry an arbitrary scale |
| Fixed 2-decimal rule, documenting the limitation | Wrong prices without any error as soon as a 0- or 3-decimal currency is configured |

## Consequences

- ➕ Deterministic, explainable prices with one rule in one place.
- ➕ Any configured currency is priced with its correct number of decimals.
- ➖ The minor-unit exceptions are a small static table that follows ISO 4217; it changes only when ISO amends the list.
