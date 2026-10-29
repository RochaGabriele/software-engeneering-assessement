# ADR-0007: Open Exchange Rates behind an anti-corruption layer; App ID kept out of source and URLs

- **Status:** Accepted
- **Date:** 2026-10-26

## Context

Task 2 integrates with Open Exchange Rates (OXR). The following facts were verified against the official documentation on 2026-10-26:

- The free plan supports **only the USD base**; changing `base` is a paid feature.
- `symbols` (a subset of currencies) **is** available on the free plan.
- The quota is 1,000 requests/month, with hourly updates.
- `timestamp` is UNIX seconds UTC.
- The App ID may be sent as the `app_id` query parameter or as the `Authorization: Token <id>` header.
- Errors come as `{ "error": true, "status", "message", "description" }`, with codes such as `invalid_app_id` and `not_allowed`.

The provider's model (a dictionary of codes to numbers plus a UNIX timestamp) must not leak into the domain. The App ID is a secret, and the repository will be shared with reviewers.

## Decision

- **Port.** `IExchangeRateProvider` lives in Application (`Modules/ExchangeRates/Ports`) and speaks domain types: `Currency`, `decimal`, `DateTimeOffset`.
- **Adapter (ACL).** `OpenExchangeRatesClient` is a typed `HttpClient` in Infrastructure. It:
  - calls `latest.json?symbols=EUR,CAD,GBP,CHF`;
  - **does not send `base`** (USD is the default, and the parameter is restricted on the free plan), and reports the base the response actually used. The sync use case, not the adapter, rejects any base other than USD: translation and business rules stay separate, and no future adapter can bypass the check;
  - deserializes `rates` straight to `decimal`;
  - maps `timestamp` to a UTC `DateTimeOffset`, and rejects a missing or out-of-range one (it must never become 1970-01-01);
  - skips and logs null, non-positive or unknown entries;
  - converts OXR error bodies, malformed JSON (bodies are read as UTF-8, as RFC 8259 requires, so a bogus charset label does not break parsing), network failures and `HttpClient.Timeout` into `ExchangeRateProviderException` carrying a clear message. A cancellation requested by the caller propagates unchanged.
- **Authentication** goes in the `Authorization: Token` header, **never** the query string, so the App ID cannot appear in URLs, HTTP logs or Hangfire job data.
- **Secret storage.**
  - The App ID comes from **user-secrets** in development or the environment variable `OpenExchangeRates__AppId` elsewhere. It is never written to `appsettings*.json`.
  - It is deliberately **not** validated at startup, so the API and the USD catalog work without a key.
  - A missing key fails the sync run before any HTTP call, with an explicit message.

## Alternatives considered

| Option | Why not chosen |
|---|---|
| `app_id` in the query string (as in the OXR samples) | The key ends up in request logs and diagnostics |
| Send `base=USD` explicitly | Redundant, and it touches a plan-restricted parameter for no benefit |
| Fail fast at startup without an App ID | Reviewers without a key could not run the API at all |
| Call OXR from the catalog endpoint | Violates "latest available exchange rate **from the database**" and would exhaust the quota |

## Consequences

- ➕ Swapping providers means writing one new adapter; domain and application are untouched.
- ➕ The secret never reaches source control or URLs.
- ➖ The reviewer must supply their own App ID (free sign-up) to exercise the live sync. All automated tests run without one.
