# Technical Assessment

Welcome to the technical assessment! This repository contains a starter project designed for evaluating candidates.

## 📌 Project Overview
This project is a simplified backend API. It provides the foundation for you to demonstrate your system design skills, coding standards, understanding of clean architecture, and ability to tackle complex backend scenarios using modern .NET technologies.

## ✅ What This Delivery Contains

| Task | Where to look |
|---|---|
| **Task 1: Code analysis** | [`Task1_CodeAnalysis.md`](Task1_CodeAnalysis.md): 26 findings with evidence, impact, recommendation and status. The ones in the path of Tasks 2–3 are fixed; each links the commit that fixes it |
| **Task 2: Exchange-rate sync** | A weekly Hangfire job (Monday 06:00 UTC) fetches USD→EUR/CAD/GBP/CHF from Open Exchange Rates. It stores the current rate per currency and an append-only history ([details](#-exchange-rate-synchronization-task-2)) |
| **Task 3: Catalog in local currency** | `GET /api/products?currency=EUR` converts every price with the latest stored rate ([details](#-product-catalog-in-local-currency-task-3)) |
| **Design and decisions** | [Design spec](docs/specs/2026-10-26-exchange-rates-catalog-design.md) · [ADRs](docs/adr/README.md) · [Implementation plan](docs/plans/2026-10-26-implementation-plan.md) |

## 🏗️ Architecture & Design
The codebase is structured following **Clean Architecture** and **Domain-Driven Design (DDD)** principles to separate concerns and ensure maintainability. Dependencies point inwards: Domain ← Application ← Infrastructure, with the API as the composition root.

* **`Demo.Api`**: The entry point of the application. It contains ASP.NET Core Controllers, Swagger configuration, the RFC 9457 ProblemDetails error contract, and the composition root (`AddApplication()` / `AddInfrastructure()`).
* **`Demo.Application`**: Contains the business orchestration logic: one handler per use case (`GetProductCatalog`, `GetProductBySku`, `SynchronizeExchangeRates`), the ports to external systems, and the read contract the catalog uses to obtain currency conversions.
* **`Demo.Domain`**: The core of the system.
  * A small shared kernel (`Currency`, `Money`, `CurrencyConversion`).
  * The Inventory context (`Product`, `Sku`).
  * The Exchange Rates context (`ExchangeRate` aggregate, `ExchangeRateHistoryEntry`), with their repository interfaces and invariants.
* **`Demo.Infrastructure`**: Implementation details:
  * Entity Framework Core (SQLite) with migrations;
  * the Open Exchange Rates client, an anti-corruption layer with HTTP resilience;
  * background job processing with Hangfire.
* **`Demo.UnitTests`**: Domain and application tests, using hand-written fakes.
* **`Demo.IntegrationTests`**: Tests against real SQLite, the HTTP client against a stubbed handler, Hangfire scheduling against real job storage, the HTTP API through `WebApplicationFactory`, and an end-to-end sync → catalog scenario.

## 🛠️ Tech Stack
* **Framework:** .NET 10.0
* **Persistence:** Entity Framework Core with SQLite and EF Core Migrations (used for simplicity so no external database setup is required)
* **Background Processing:** Hangfire (configured with Memory Storage)
* **External data:** [Open Exchange Rates](https://openexchangerates.org/) through a typed `HttpClient` with `Microsoft.Extensions.Http.Resilience`
* **Testing:** xUnit, `Microsoft.AspNetCore.Mvc.Testing`
* **Package management:** Central Package Management (`Directory.Packages.props`)

## 🚀 Getting Started

### Prerequisites
* [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
* An IDE of your choice (Visual Studio 2022, JetBrains Rider, or VS Code)
* Optional, to fetch real exchange rates: a free [Open Exchange Rates App ID](https://openexchangerates.org/signup/free)

### Running the Application

1. **Restore dependencies and build the solution** (from the repository root):
   ```bash
   dotnet build
   ```

2. **Configure the Open Exchange Rates App ID** (optional; see [below](#configuring-open-exchange-rates)):
   ```bash
   dotnet user-secrets set "OpenExchangeRates:AppId" "<your-app-id>" --project src/Demo.Api
   ```

3. **Run the API:**
   Navigate to the API folder and start the application. The SQLite database is created by EF Core migrations and seeded with initial data on startup.
   ```bash
   cd src/Demo.Api
   dotnet run
   ```

4. **Useful Endpoints** (`http` profile: port 5022; `https` profile: port 7246):
   * **Swagger UI:** `https://localhost:<port>/swagger` (or `http://localhost:<port>/swagger`) - Explore and test the API endpoints.
   * **Hangfire Dashboard:** `https://localhost:<port>/hangfire` - Monitor background jobs and trigger the exchange-rate sync manually.
   * **Catalog:** `GET /api/products[?currency=EUR]` and `GET /api/products/{sku}`.

### Configuring Open Exchange Rates

The App ID is a secret and is **never** stored in `appsettings*.json`. Provide it through one of:

| Where | How |
|---|---|
| Local development | `dotnet user-secrets set "OpenExchangeRates:AppId" "<your-app-id>" --project src/Demo.Api` |
| Any environment | Environment variable `OpenExchangeRates__AppId=<your-app-id>` |

Without an App ID the API still starts and serves the catalog in USD. Only the sync fails, with the message *"The Open Exchange Rates App ID is not configured…"*. Hangfire retries the run and, after the last attempt, lists it under *Failed* in the dashboard. No request is sent to the provider. The client sends the App ID in the `Authorization: Token …` header, never in the URL, so it cannot leak into request logs.

### Running Tests
To execute the tests, run the following command from the root directory:
```bash
dotnet test
```
No test calls the real Open Exchange Rates API, and no test needs an App ID.

### Database Migrations
The schema is managed by EF Core Migrations in `src/Demo.Infrastructure/Persistence/Migrations`, and they are applied automatically at startup. To create a new migration, use the locally pinned `dotnet-ef`:
```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/Demo.Infrastructure --startup-project src/Demo.Api --output-dir Persistence/Migrations
```

> **Upgrading from the original starter:** delete any existing `src/Demo.Api/demo.db*` files first. That database was created with `EnsureCreated` and has no migration history, so applying migrations to it fails with "table already exists". A fresh clone is not affected, because the file is git-ignored.

---

## 💱 Exchange-Rate Synchronization (Task 2)

| Aspect | Behavior |
|---|---|
| Source | Open Exchange Rates `latest.json?symbols=EUR,CAD,GBP,CHF`. USD is the base: it is the default, and the only base on the free plan |
| Schedule | Hangfire recurring job `exchange-rates-sync`, **every Monday at 06:00 UTC** (`ExchangeRates:Sync:Cron` / `TimeZone`) |
| First run | If no rate is stored when the app starts, one run is enqueued immediately, so a fresh deployment does not wait until Monday (`RunOnStartupWhenEmpty`) |
| Manual run | Hangfire dashboard → *Recurring Jobs* → `exchange-rates-sync` → **Trigger now** |
| Storage | `ExchangeRates`: one row per pair, **updated** with the latest rate. `ExchangeRateHistory`: append-only **log** of every applied publication |
| Idempotency | A publication is applied only if it is newer than the stored one. A unique index on (pair, publication time) guarantees the history never duplicates, whatever the number of reruns |
| Failures | Transient HTTP errors are retried with backoff and jitter; a failed run is retried by Hangfire (3 attempts); overlapping runs are prevented. A partial response applies the currencies that arrived and logs the missing ones |
| Precision | Rates stay `decimal` from JSON to database (stored as exact TEXT in SQLite), never `double` |

The supported currencies are configured in `ExchangeRates:TargetCurrencies` and validated at startup. The same list drives both the sync and the catalog.

## 🛒 Product Catalog in Local Currency (Task 3)

```bash
curl http://localhost:5022/api/products
curl "http://localhost:5022/api/products?currency=EUR"
```

```json
[
  { "sku": "SKU1", "name": "Name", "price": 95.04, "currency": "EUR" },
  { "sku": "SKU2", "name": "Name", "price": 94.02, "currency": "EUR" }
]
```

* No `currency`, an empty value or `USD` returns the stored USD prices. The parameter is case-insensitive (`eur` works).
* Conversion: `price × latest stored USD→currency rate`. The rate is used at full precision and the result is rounded **once** to 2 decimals, half away from zero (`103.30 × 0.92 = 95.036 → 95.04`). Prices always serialize with 2 decimals.
* The endpoint reads rates from the database only. It never calls the external provider.

Errors use RFC 9457 `application/problem+json`, with a stable `code` extension and the `traceId`:

| Request | Status | `code` |
|---|---|---|
| `?currency=EURO` (not an ISO 4217 code) | 400 | `currency.invalid` |
| `?currency=JPY` (not supported) | 400 | `currency.unsupported` |
| `?currency=CAD` before the first sync stored a CAD rate | 503 | `exchange_rate.unavailable` |
| `GET /api/products/UNKNOWN` | 404 | `product.not_found` |
| Unexpected failure | 500 | (no internal details exposed) |

---

## 📝 Candidate Instructions

Your assessment is broken down into specific tasks. Please start by reading:

👉 **[Task 1: Code Analysis & Refactoring Identification](Task1_CodeAnalysis.md)**

👉 **[Task 2: Exchange Rate Synchronization](Task2_ExchangeRates.md)**

👉 **[Task 3: Product Catalog with Currency Conversion](Task3_ProductCatalogCurrency.md)**

Please complete the tasks assigned above. You are expected to treat this codebase as if it were a real production system.

**Note:** There is no single "correct" answer for these tasks. What matters most is your ability to explain your reasoning and justify the technical decisions you make. Additionally, there are no restrictions on the tools or resources you use to complete this assessment, feel free to use whatever helps you deliver your best work.

**Good luck!**
