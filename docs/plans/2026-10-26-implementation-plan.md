# Implementation Plan: Exchange Rates & Multi-Currency Catalog

| | |
|---|---|
| **Spec** | [`docs/specs/2026-10-26-exchange-rates-catalog-design.md`](../specs/2026-10-26-exchange-rates-catalog-design.md) |
| **Decisions** | [`docs/adr/`](../adr/README.md) |
| **Baseline** | `a456096` |
| **Date** | 2026-10-26 |

**Goal.** Deliver Tasks 1–3 as specified. Every requirement in spec §2 is traceable to a passing test, and every commit leaves the solution building and the tests passing.

**Approach.**
- Bottom-up, with the dependency rule enforced: shared kernel → domain → application → infrastructure → API.
- Test-first throughout: write the failing test, watch it fail for the expected reason, write the minimal code, watch it pass, then commit.
- Each task ends in one conventional commit.

**Stack.** .NET 10 SDK, ASP.NET Core, EF Core 10.0.14 (SQLite), Hangfire 1.8 (MemoryStorage), `Microsoft.Extensions.Http.Resilience`, xUnit 2.9, `Microsoft.AspNetCore.Mvc.Testing`.

---

## 0. Conventions

- **Namespaces** are file-scoped and mirror folders (`Demo.Domain.Modules.ExchangeRates`, …).
- **Services** use constructor injection into `private readonly` fields named `_camelCase`, matching the existing code.
- **Async**: every async method ends in `Async` and takes a `CancellationToken cancellationToken` as its last parameter.
- **Tests** are named `Method_Scenario_ExpectedOutcome`. Assertions use xUnit `Assert`. Fakes are hand-written and live in `Fakes/` folders. There is no mocking library.
- **Verification commands** always run from the repository root:
  - `dotnet build` → `0 Warning(s)`, `0 Error(s)`
  - `dotnet test` → all passed
- **Commits** use the type prefixes `feat`, `fix`, `refactor`, `test`, `docs`, `build`, `chore`. The body explains *why*.
- **Out of scope**, so do not add: MediatR, AutoMapper, FluentAssertions, mocking libraries, domain events, caching, pagination, authentication.

## 1. File map (end state)

```
DemoSolution.slnx                        (moved from src/)
Directory.Build.props                    NEW
Directory.Packages.props                 NEW
.gitattributes                           NEW
dotnet-tools.json                NEW  (dotnet-ef 10.0.14)
src/Demo.Domain/
  Common/DomainException.cs              NEW
  Common/Currency.cs                     NEW
  Common/Money.cs                        NEW
  Common/CurrencyConversion.cs           NEW
  Common/Services/IMessagePublisher.cs   unchanged (documented only)
  Modules/Inventory/Sku.cs               NEW
  Modules/Inventory/Product.cs           REWRITTEN
  Modules/Inventory/IProductRepository.cs  REWRITTEN
  Modules/ExchangeRates/CurrencyPair.cs  NEW
  Modules/ExchangeRates/ExchangeRate.cs  NEW
  Modules/ExchangeRates/ExchangeRateHistoryEntry.cs  NEW
  Modules/ExchangeRates/IExchangeRateRepository.cs   NEW
  Modules/ExchangeRates/IExchangeRateHistoryRepository.cs  NEW
src/Demo.Application/
  DependencyInjection.cs                 NEW
  Common/Error.cs  Common/Result.cs  Common/IQueryHandler.cs  Common/ICommandHandler.cs  Common/IUnitOfWork.cs  NEW
  Modules/InventoryModule.cs             DELETED
  Modules/Inventory/ProductDto.cs  Modules/Inventory/InventoryErrors.cs  NEW
  Modules/Inventory/GetProductBySku/GetProductBySkuQuery.cs + GetProductBySkuQueryHandler.cs  NEW
  Modules/Inventory/GetProductCatalog/GetProductCatalogQuery.cs + GetProductCatalogQueryHandler.cs  NEW
  Modules/ExchangeRates/ExchangeRatesOptions.cs  ExchangeRatesOptionsValidator.cs  SupportedCurrencies.cs  ExchangeRateErrors.cs  NEW
  Modules/ExchangeRates/Contracts/ICurrencyConversionReader.cs + CurrencyConversionReader.cs  NEW
  Modules/ExchangeRates/Ports/IExchangeRateProvider.cs  LatestRates.cs  ExchangeRateProviderException.cs  NEW
  Modules/ExchangeRates/SynchronizeExchangeRates/SynchronizeExchangeRatesCommand.cs  SynchronizationReport.cs  SynchronizeExchangeRatesCommandHandler.cs  NEW
src/Demo.Infrastructure/
  DependencyInjection.cs                 NEW
  HangfireSetup.cs                       unchanged
  Messaging/FakeMessagePublisher.cs      unchanged
  Persistence/DemoDbContext.cs           REWRITTEN (: IUnitOfWork, configurations from assembly)
  Persistence/DatabaseMigration.cs       NEW  (MigrateDatabaseAsync extension)
  Persistence/DemoDataSeeder.cs          NEW
  Persistence/ProductRepository.cs       REWRITTEN
  Persistence/ExchangeRateRepository.cs  ExchangeRateHistoryRepository.cs  NEW
  Persistence/Converters/CurrencyConverter.cs  SkuConverter.cs  UnixMillisecondsConverter.cs  NEW
  Persistence/Configurations/ProductConfiguration.cs  ExchangeRateConfiguration.cs  ExchangeRateHistoryEntryConfiguration.cs  NEW
  Persistence/Migrations/*               GENERATED (InitialCreate)
  ExchangeRates/OpenExchangeRates/OpenExchangeRatesOptions.cs  OpenExchangeRatesClient.cs  OpenExchangeRatesResponses.cs  NEW
  BackgroundJobs/ExchangeRateSyncOptions.cs  ExchangeRatesSyncJob.cs  ExchangeRatesSyncScheduler.cs  NEW
src/Demo.Api/
  Program.cs                             REWRITTEN (composition root only; `public partial class Program`)
  Controllers/ProductsController.cs      REWRITTEN (namespace Demo.Api.Controllers, class ProductsController)
  ErrorHandling/ErrorResults.cs          NEW
  appsettings.json                       MODIFIED
src/Demo.UnitTests/                      Domain/…, Application/…, Fakes/…  (UnitTest1.cs DELETED)
src/Demo.IntegrationTests/               NEW project: Persistence/…, OpenExchangeRates/…, BackgroundJobs/…, Api/…, EndToEnd/…
```

---

## Task 1: Document Task 1 findings (baseline analysis)

**Files:** modify `Task1_CodeAnalysis.md`.

Findings describe the baseline (`a456096`). The *Status* column starts as `Planned: Task N` or `Recommended` and gets the fixing commit hash in Task 17.

Each finding gets a section with: **ID · Severity · Location (file:line at `a456096`) · Observation (with evidence) · Impact · Recommendation · Status**. Severity scale: *High* = incorrect behavior, data loss or security exposure; *Medium* = maintainability or operability risk; *Low* = hygiene.

- [ ] **Step 1: Write the summary table and one section per finding**

| ID | Sev. | Area | Finding (location) | Planned disposition |
|---|---|---|---|---|
| F-01 | High | Correctness | Unknown SKU → **500 with stack trace**: `FirstAsync` throws `InvalidOperationException`, and there is no error contract (`ProductRepository.cs:17`, observed with `GET /api/products/NOPE`) | Task 9 (404 ProblemDetails) |
| F-02 | High | Data | Prices stored as **binary floating point**: `HasColumnType("decimal(18,2)")` gives NUMERIC affinity in SQLite, and `typeof(Price)` returns `real` (`DemoDbContext.cs:23`) | Task 9 (`HasPrecision` → TEXT) |
| F-03 | High | Data | `EnsureCreated()` cannot evolve the schema, so new tables are never created on an existing DB (`Program.cs:51`) | Task 10 (Migrations) |
| F-04 | High | Security | 3 vulnerable packages (8 × NU1903): Microsoft.OpenApi 2.4.1 (GHSA-v5pm-xwqc-g5wc), Newtonsoft.Json 11.0.1 via Hangfire (GHSA-5crp-9r3c-p9vr), SQLitePCLRaw.lib.e_sqlite3 2.1.11 (GHSA-2m69-gcr7-jv3q) | Task 2 |
| F-05 | High | DDD | Anemic `Product`: public setters, no invariants (empty SKU/name, negative price accepted), primitive obsession (SKU `string`, price `decimal` with no currency) (`Product.cs:3-8`) | Task 9 |
| F-06 | Medium | Architecture | API returns the domain entity, which couples the HTTP contract to the domain model (`ProductsController.cs:20-25`) | Task 9 (DTO) |
| F-07 | Medium | Architecture | `Program.cs` registers concrete infrastructure types and hard-codes the connection string. The composition root knows every implementation (`Program.cs:17-29`) | Task 3 |
| F-08 | Medium | Architecture | `IInventoryModule` facade is a pass-through with an unused `_logger`. Facades grow with every use case (SRP/ISP) (`InventoryModule.cs`) | Task 9 (use-case handlers) |
| F-09 | Medium | Architecture | `IMessagePublisher` lives in Domain (messaging is not a domain concept). It is never used, `FakeMessagePublisher` silently drops messages in every environment, and there is no outbox (`Domain/Common/Services/IMessagePublisher.cs`, `Program.cs:23`) | Recommended |
| F-10 | Medium | Operability | Seeding inline in `Program.cs` with synchronous EF calls, mixed with bootstrapping. Seed data is low quality (both products named "Name") (`Program.cs:47-75`) | Task 9 (`UseSeeding`). Names kept for compatibility; recommendation recorded |
| F-11 | Medium | Correctness | No `CancellationToken` propagation; async methods without `Async` suffix (`IProductRepository.cs:5`, `InventoryModule.cs:25`) | Task 9 |
| F-12 | Medium | Tooling | README build/test commands fail **from the repository root** as documented (`MSB1003`): the solution lives in `src/` | Task 2 |
| F-13 | Medium | Tests | No tests: `UnitTest1.Test1` is empty, and the test project references only Application | Tasks 4–14 |
| F-14 | Medium | Operability | Hangfire dashboard relies on the default local-only filter. Behind a reverse proxy on the same host every request looks local, which exposes job control. No authorization policy (`Program.cs:43`) | Recommended |
| F-15 | Medium | Operability | Hangfire MemoryStorage: jobs and history are lost on restart, and with several instances each runs its own copy of every recurring job (`HangfireSetup.cs:16`) | Recommended (registration on every startup in Task 12) |
| F-16 | Medium | Security | No authentication/authorization on the API | Recommended |
| F-17 | Low | Naming | Namespace `Ecommerce.Api.Controllers` in project `Demo.Api`; class `ProductController` in file `ProductsController.cs` | Task 9 |
| F-18 | Low | API docs | Swagger shows only `200` with no schema (no `ProducesResponseType`) | Tasks 9, 13 |
| F-19 | Low | Dependencies | `Microsoft.AspNetCore.OpenApi` referenced but unused (no `AddOpenApi`/`MapOpenApi`) alongside Swashbuckle | Task 2 |
| F-20 | Low | DI | Repository/module registered `Transient` while `DbContext` is `Scoped`. It works, but `Scoped` is the idiomatic lifetime for units of work | Task 3 |
| F-21 | Low | Build | TFM, `Nullable` and `ImplicitUsings` duplicated in every `.csproj`; package versions scattered | Task 2 |
| F-22 | Low | Hygiene | `.DS_Store` committed; no `.gitattributes` in a repo edited on macOS and Windows | Task 2 |
| F-23 | Low | HTTP | `UseHttpsRedirection` with the HTTP-only profile logs `Failed to determine the https port for redirect` (observed). No HSTS | Recommended |
| F-24 | Low | Operability | No health checks and no tracing/metrics | Recommended |
| F-25 | Low | Scalability | SQLite (single writer, file-based) and no pagination are acceptable for the assessment, not for production | Recommended |
| F-26 | Low | Language | "Inventory" module holds the product catalog (no stock concept), which diverges from the ubiquitous language of Task 3 | Recommended (rename when a real inventory concept appears) |

- [ ] **Step 2:** Add a closing *Prioritized recommendations* list (F-14, F-15, F-16, F-09, F-24, F-25, F-23, F-26), each with one sentence on *what to do first and why*.
- [ ] **Step 3: Commit** `docs(task1): document code analysis findings`.

---

## Task 2: Build and repository hygiene

**Files:**
- move `src/DemoSolution.slnx` → `DemoSolution.slnx`
- create `Directory.Build.props`, `Directory.Packages.props`, `.gitattributes`
- modify every `src/**/*.csproj`, `.gitignore`
- delete `.DS_Store`

- [ ] **Step 1: Prove the defect.** Run `dotnet build` from the root. Expected: `MSB1003`.
- [ ] **Step 2: Move the solution:** `git mv src/DemoSolution.slnx DemoSolution.slnx`. Prefix every path with `src/`:

```xml
<Solution>
  <Project Path="src/Demo.Api/Demo.Api.csproj" />
  <Project Path="src/Demo.Application/Demo.Application.csproj" />
  <Project Path="src/Demo.Domain/Demo.Domain.csproj" />
  <Project Path="src/Demo.Infrastructure/Demo.Infrastructure.csproj" />
  <Project Path="src/Demo.UnitTests/Demo.UnitTests.csproj" />
</Solution>
```

- [ ] **Step 3: `Directory.Build.props`.** Shared properties. Warnings are deliberately *not* escalated to errors (spec NFR-02).

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

- [ ] **Step 4: `Directory.Packages.props`** with Central Package Management and transitive pinning. Only security-motivated bumps; other baseline versions are kept.

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
  <ItemGroup>
    <!-- 10.0.5 → 10.0.14: resolves SQLitePCLRaw 2.1.13 (GHSA-2m69-gcr7-jv3q affects <= 2.1.11) -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.14" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.14" />
    <PackageVersion Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.14" />
    <!-- 10.1.7 → 10.3.1: resolves Microsoft.OpenApi 2.7.5 (GHSA-v5pm-xwqc-g5wc affects <= 2.7.4) -->
    <PackageVersion Include="Swashbuckle.AspNetCore" Version="10.3.1" />
    <PackageVersion Include="Hangfire.AspNetCore" Version="1.8.23" />
    <PackageVersion Include="Hangfire.MemoryStorage" Version="1.8.1.2" />
    <!-- Transitive pin: Hangfire.Core depends on Newtonsoft.Json >= 11.0.1 (GHSA-5crp-9r3c-p9vr affects < 13.0.1) -->
    <PackageVersion Include="Newtonsoft.Json" Version="13.0.4" />
    <PackageVersion Include="coverlet.collector" Version="6.0.4" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Clean up the `.csproj` files.**
  - Remove `TargetFramework`/`Nullable`/`ImplicitUsings` and every `Version="…"` attribute.
  - Remove `Microsoft.AspNetCore.OpenApi` from `Demo.Api.csproj` (F-19).
  - Keep `IsPackable=false` in the test project.
- [ ] **Step 6: Hygiene.**
  - `git rm --cached .DS_Store` and delete the file.
  - Append `.DS_Store` to `.gitignore`.
  - Create `.gitattributes` containing `* text=auto`.
- [ ] **Step 7: Verify.**

| Command (from root) | Expected |
|---|---|
| `dotnet build` | `0 Warning(s)` `0 Error(s)` |
| `dotnet test` | `Passed: 1` (placeholder test still present) |
| `dotnet list package --vulnerable --include-transitive` | "has no vulnerable packages" for every project |

- [ ] **Step 8: Commit** `build: move solution to root, centralize build settings and fix vulnerable packages`.

---

## Task 3: Composition root

**Files:**
- create `src/Demo.Application/DependencyInjection.cs` and `src/Demo.Infrastructure/DependencyInjection.cs`
- modify `src/Demo.Api/Program.cs` and `src/Demo.Api/appsettings.json`

Behavior must be unchanged. Seeding stays in `Program.cs` until Task 9.

- [ ] **Step 1: `appsettings.json`:** add `"ConnectionStrings": { "Demo": "Data Source=demo.db" }`.
- [ ] **Step 2: `AddApplication`** registers `IInventoryModule` (it is deleted in Task 9).

```csharp
namespace Demo.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IInventoryModule, InventoryModule>();
        return services;
    }
}
```

- [ ] **Step 3: `AddInfrastructure`.** The connection string is read **lazily** inside the options callback, so test hosts can override it (Task 13).

```csharp
namespace Demo.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Demo";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DemoDbContext>((serviceProvider, options) =>
        {
            var connectionString = serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");
            options.UseSqlite(connectionString);
        });
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IMessagePublisher, FakeMessagePublisher>();
        services.AddInfrastructureHangfire();
        return services;
    }
}
```

`Demo.Application.csproj` gets `Microsoft.Extensions.Options.ConfigurationExtensions` (CPM `10.0.14`). It provides `IConfiguration`, `IServiceCollection`, options binding and `IValidateOptions` for this and later tasks. It flows transitively to Infrastructure through the project reference.

- [ ] **Step 4: `Program.cs`** calls `builder.Services.AddApplication(builder.Configuration)` and `AddInfrastructure(builder.Configuration)` in place of the inline registrations. The seed block stays.
- [ ] **Step 5: Verify.**
  - `dotnet build` and `dotnet test` from the root are green.
  - Delete `src/Demo.Api/demo.db*`, run `cd src/Demo.Api && dotnet run --launch-profile http`.
  - `curl http://localhost:5022/api/products/SKU1` → `200 {"sku":"SKU1","name":"Name","price":103.3}`.
- [ ] **Step 6: Commit** `refactor: move service registration into layer composition methods`.

---

## Task 4: Shared kernel (`Currency`, `Money`, `CurrencyConversion`)

**Files:**
- create `src/Demo.Domain/Common/{DomainException,Currency,Money,CurrencyConversion}.cs`
- tests in `src/Demo.UnitTests/Domain/Common/{CurrencyTests,MoneyTests,CurrencyConversionTests}.cs`
- delete `src/Demo.UnitTests/UnitTest1.cs`

Contracts: Appendix A.1.

- [ ] **Step 1: Write the failing tests.**

| Test | Input | Expected |
|---|---|---|
| `TryParse_ValidCode_NormalizesToUpperCase` | `" eur "` | `true`, `Code == "EUR"` |
| `TryParse_InvalidInput_ReturnsFalse` (Theory) | `null`, `""`, `"  "`, `"EU"`, `"EURO"`, `"12A"`, `"€UR"` | `false` |
| `Create_InvalidCode_ThrowsDomainException` | `"EURO"` | `DomainException` |
| `Equality_SameCode_AreEqual` | `Create("usd")`, `Currency.USD` | equal, same hash code |
| `Money_RoundToMinorUnits_NormalizesScale` | `103.3m USD` | amount `ToString(InvariantCulture) == "103.30"` |
| `Money_RoundToMinorUnits_RoundsHalfAwayFromZero` | `0.125m` | `0.13m` (banker's would give `0.12`) |
| `Convert_MultipliesByRate` (direction) | `100m USD`, USD→EUR `0.92` | `92.00m EUR` |
| `Convert_RoundsOnceAtTheEnd` | `103.30m USD`, rate `0.921234` | `95.16m` (`95.163472…`) |
| `Convert_SpecExample` | `103.30m USD`, rate `0.92` | `95.04m` (`95.036`) |
| `Convert_CurrencyMismatch_Throws` | `10m EUR` with a USD→EUR conversion | `DomainException` |
| `Identity_ReturnsSameAmountNormalized` | `102.2m USD`, `Identity(USD)` | `"102.20"`, `USD`, `AsOf == null` |
| `Ctor_NonPositiveRate_Throws` (Theory) | `0m`, `-1m` | `DomainException` |

- [ ] **Step 2:** Run `dotnet test --filter "FullyQualifiedName~Domain.Common"`. Expected: compile failure, because the types do not exist yet.
- [ ] **Step 3: Implement per Appendix A.1.** The rounding helper is the single rounding rule of the system (ADR-0008):

```csharp
public Money RoundToMinorUnits() =>
    // Adding 0.00m forces scale 2, so 103.3 is represented (and serialized) as 103.30.
    new(decimal.Round(Amount, MinorUnits, MidpointRounding.AwayFromZero) + 0.00m, Currency);
```

- [ ] **Step 4:** Run the tests. Expected: all pass. Run `dotnet build`. Expected: 0 warnings.
- [ ] **Step 5: Commit** `feat(domain): add shared kernel money value objects`.

---

## Task 5: Exchange Rates domain

**Files:**
- create `src/Demo.Domain/Modules/ExchangeRates/{CurrencyPair,ExchangeRate,ExchangeRateHistoryEntry,IExchangeRateRepository,IExchangeRateHistoryRepository}.cs`
- tests in `src/Demo.UnitTests/Domain/ExchangeRates/{CurrencyPairTests,ExchangeRateTests,ExchangeRateHistoryEntryTests}.cs`

Contracts: Appendix A.2. `t0 = 2026-10-26T06:00:00Z`.

- [ ] **Step 1: Write the failing tests.**

| Test | Scenario | Expected |
|---|---|---|
| `CurrencyPair_SameCurrencies_Throws` | USD/USD | `DomainException` |
| `CurrencyPair_ToString` | USD/EUR | `"USD/EUR"` |
| `Create_SetsState` | USD/EUR, `0.92`, `t0`, `t0+1m` | properties match; `Pair == USD/EUR` |
| `Create_NonPositiveRate_Throws` | rate `0` | `DomainException` |
| `Update_NewerPublication_AppliesAndReturnsTrue` | stored `t0`; update `0.93` at `t0+7d` | `true`; `Rate == 0.93`; `PublishedAt == t0+7d` |
| `Update_SamePublication_IgnoredAndReturnsFalse` | update `0.99` at `t0` | `false`; state unchanged |
| `Update_OlderPublication_IgnoredAndReturnsFalse` | update at `t0-1d` | `false`; state unchanged |
| `Update_NonPositiveRate_Throws` | newer `t0+1d`, rate `-1` | `DomainException` |
| `ToConversion_MapsFields` | USD/EUR `0.92` at `t0` | `From USD`, `To EUR`, `Rate 0.92`, `AsOf t0` |
| `HistoryEntry_Record_CopiesCurrentState` | from a created rate | same pair/rate/timestamps; `Id == 0` |

- [ ] **Step 2:** Run the tests. Expected: compile failure.
- [ ] **Step 3:** Implement per Appendix A.2.
- [ ] **Step 4:** Run the tests. Expected: pass.
- [ ] **Step 5: Commit** `feat(domain): add exchange rate aggregate and history entry`.

---

## Task 6: Application building blocks

**Files:**
- create `src/Demo.Application/Common/{Error,Result,IQueryHandler,ICommandHandler,IUnitOfWork}.cs`
- tests in `src/Demo.UnitTests/Application/Common/ResultTests.cs`

Contracts: Appendix A.3.

- [ ] **Step 1: Write the failing tests.**

| Test | Expected |
|---|---|
| `Success_ExposesValue` | `IsSuccess`, `Value` set, `Error == null` |
| `Failure_ExposesError` | `!IsSuccess`, `Error` set |
| `Failure_AccessingValue_Throws` | `InvalidOperationException` |
| `ImplicitConversionFromError_CreatesFailure` | `Result<int> r = Error.NotFound("x", "y")` → failure |
| `ErrorFactories_SetType` | `Validation`/`NotFound`/`Unavailable` set `Type` |

- [ ] **Steps 2–4:** red → implement → green.
- [ ] **Step 5: Commit** `feat(application): add result, error and use-case abstractions`.

---

## Task 7: Supported currencies and the conversion contract

**Files:**
- create `src/Demo.Application/Modules/ExchangeRates/{ExchangeRatesOptions,ExchangeRatesOptionsValidator,SupportedCurrencies,ExchangeRateErrors}.cs`
- create `…/Contracts/{ICurrencyConversionReader,CurrencyConversionReader}.cs`
- tests in `src/Demo.UnitTests/Application/ExchangeRates/{ExchangeRatesOptionsValidatorTests,CurrencyConversionReaderTests}.cs`
- fakes in `src/Demo.UnitTests/Fakes/InMemoryExchangeRateRepository.cs`

Not registered in DI yet: the repository implementation arrives in Task 10. Development builds validate the container on startup, so registering early would break `dotnet run`.

- [ ] **Step 1: Write the failing tests.**

| Test | Scenario | Expected |
|---|---|---|
| `Validator_ValidTargets_Succeeds` | `["EUR","CAD","GBP","CHF"]` | success |
| `Validator_Empty_Fails` | `[]` | fails, message mentions `TargetCurrencies` |
| `Validator_InvalidCode_Fails` | `["EURO"]` | fails, names `EURO` |
| `Validator_ContainsBase_Fails` | `["USD","EUR"]` | fails, names `USD` |
| `Validator_Duplicates_Fails` | `["EUR","eur"]` | fails |
| `Reader_SupportedCurrencies_IsBasePlusTargets` | targets EUR, CAD | `[USD, EUR, CAD]` |
| `Reader_SameCurrency_ReturnsIdentityWithoutStorage` | USD→USD | success, `Rate 1`; repository call count `0` |
| `Reader_StoredRate_ReturnsConversion` | USD/EUR stored `0.92` | success, `Rate 0.92`, `AsOf` = published |
| `Reader_NoStoredRate_ReturnsUnavailable` | USD→CAD, nothing stored | `ErrorType.Unavailable`, code `exchange_rate.unavailable`, message contains `USD/CAD` |
| `Reader_UnsupportedTarget_ReturnsValidation` | USD→JPY | code `currency.unsupported`, message lists `USD, EUR, CAD` |
| `Reader_FromNonBase_ReturnsValidation` | EUR→CAD | code `conversion.unsupported` |

- [ ] **Steps 2–4:** red → implement per Appendix A.4 → green.
- [ ] **Step 5: Commit** `feat(exchange-rates): add supported currencies and conversion reader contract`.

---

## Task 8: Synchronize-exchange-rates use case

**Files:**
- create `src/Demo.Application/Modules/ExchangeRates/Ports/{IExchangeRateProvider,LatestRates,ExchangeRateProviderException}.cs`
- create `…/SynchronizeExchangeRates/{SynchronizeExchangeRatesCommand,SynchronizationReport,SynchronizeExchangeRatesCommandHandler}.cs`
- tests in `src/Demo.UnitTests/Application/ExchangeRates/SynchronizeExchangeRatesCommandHandlerTests.cs`
- fakes in `src/Demo.UnitTests/Fakes/{StubExchangeRateProvider,InMemoryExchangeRateHistoryRepository,SpyUnitOfWork,FixedTimeProvider}.cs`

Contracts: Appendix A.5. Targets EUR, CAD, GBP, CHF. `t0 = 2026-10-26T06:00:00Z`; clock = `t0 + 5 min`.

- [ ] **Step 1: Write the failing tests.**

| Test | Given | Expected |
|---|---|---|
| `FirstRun_CreatesRatesAndHistory` | empty store; provider returns 4 rates at `t0` | 4 rates and 4 history entries; `Applied = [EUR,CAD,GBP,CHF]`; `SaveChanges` called **once**; `RetrievedAt == clock` |
| `NewerPublication_UpdatesAndAppendsHistory` | stored at `t0`; provider at `t0+7d` with EUR `0.93` | EUR rate `0.93`; history +4; `Applied` has 4 |
| `SamePublication_IsIdempotent` | stored at `t0`; provider at `t0` | no history added; `Unchanged` has 4; `SaveChanges` **not** called; success |
| `PartialResponse_AppliesPresentAndReportsMissing` | provider omits GBP | 3 applied; `Missing = [GBP]`; success |
| `NonPositiveRate_TreatedAsMissing` | CHF `0` | `Missing` contains CHF |
| `NothingApplicable_ReturnsFailure` | provider returns no target rates | failure, `ErrorType.Unavailable`, code `exchange_rate.no_rates_applied`; `SaveChanges` not called |
| `ProviderThrows_Propagates` | provider throws `ExchangeRateProviderException` | the same exception propagates; nothing saved |
| `RequestsBaseAndConfiguredTargets` | any | provider received `USD` and `[EUR,CAD,GBP,CHF]` |

- [ ] **Steps 2–4:** red → implement per Appendix A.5 → green.
- [ ] **Step 5: Commit** `feat(exchange-rates): add synchronize exchange rates use case`.

---

## Task 9: Rich `Product`, use-case handlers and the error contract

This task changes the shape of `Product`, so it also updates every caller to keep the build green.

**Files:**
- Domain: create `Modules/Inventory/Sku.cs`; rewrite `Product.cs` and `IProductRepository.cs`.
- Application: create `Modules/Inventory/{ProductDto,InventoryErrors}.cs`, `GetProductBySku/*`, `GetProductCatalog/*`; delete `Modules/InventoryModule.cs`; update `DependencyInjection.cs`.
- Infrastructure:
  - create `Persistence/Converters/{SkuConverter,CurrencyConverter}.cs`, `Persistence/Configurations/ProductConfiguration.cs`, `Persistence/DemoDataSeeder.cs`;
  - rewrite `Persistence/ProductRepository.cs`;
  - update `DemoDbContext.cs` (apply configurations from the assembly) and `DependencyInjection.cs` (`UseSeeding`/`UseAsyncSeeding`).
- Api: rewrite `Controllers/ProductsController.cs`; create `ErrorHandling/ErrorResults.cs`; update `Program.cs`.
- Tests: `Domain/Inventory/{SkuTests,ProductTests}.cs`, `Application/Inventory/{GetProductBySkuQueryHandlerTests,GetProductCatalogQueryHandlerTests}.cs`, `Fakes/InMemoryProductRepository.cs`.

Contracts: Appendix A.6–A.8.

- [ ] **Step 1: Domain tests (red).**

| Test | Expected |
|---|---|
| `Sku_Create_Trims` | `" SKU1 "` → `"SKU1"` |
| `Sku_Invalid_Throws` (Theory) | `""`, `"  "`, 65 chars → `DomainException` |
| `Sku_TryCreate_Invalid_ReturnsFalse` | 65 chars → `false` |
| `Product_Create_Valid` | `SKU1`, `"Name"`, `103.30 USD` → properties set |
| `Product_EmptyName_Throws` / `Product_NameTooLong_Throws` (201 chars) | `DomainException` |
| `Product_NegativePrice_Throws` | `-0.01 USD` → `DomainException` |
| `Product_NonBaseCurrency_Throws` | `10 EUR` → `DomainException` |

- [ ] **Step 2: Application tests (red).** `GetProductCatalog` uses the real `CurrencyConversionReader` over `InMemoryExchangeRateRepository`. Seed products: SKU2 `102.20`, SKU1 `103.30` (inserted out of order on purpose). USD/EUR rate `0.92`.

| Test | Query | Expected |
|---|---|---|
| `BySku_Found_ReturnsDto` | `"SKU1"` | `ProductDto("SKU1","Name",103.30m,"USD")`, price string `"103.30"` |
| `BySku_Unknown_ReturnsNotFound` | `"NOPE"` | code `product.not_found` |
| `BySku_InvalidSku_ReturnsNotFound` | 65 chars | code `product.not_found` |
| `Catalog_NoCurrency_ReturnsUsdOrderedBySku` | `null` | `[SKU1 103.30 USD, SKU2 102.20 USD]` |
| `Catalog_EmptyCurrency_ReturnsUsd` | `"  "` | USD prices |
| `Catalog_ExplicitUsd_DoesNotReadRates` | `"USD"` | USD prices; rate repository call count `0` |
| `Catalog_Eur_ConvertsWithLatestRate` | `"eur"` | `[SKU1 95.04 EUR, SKU2 94.02 EUR]` |
| `Catalog_Malformed_ReturnsInvalidCurrency` | `"EURO"` | code `currency.invalid`; message lists supported codes |
| `Catalog_Unsupported_ReturnsUnsupported` | `"JPY"` | code `currency.unsupported` |
| `Catalog_NoRate_ReturnsUnavailable` | `"CAD"` | code `exchange_rate.unavailable`; product repository **not** read |

- [ ] **Step 3:** Implement domain and application per the appendix. Delete `InventoryModule.cs`. `AddApplication` registers `IQueryHandler<GetProductBySkuQuery, Result<ProductDto>>` → `GetProductBySkuQueryHandler` (scoped). The catalog handler is registered in Task 13.
- [ ] **Step 4: Infrastructure adaptation.**
  - `ProductConfiguration` per Appendix B.1. Use `HasPrecision(18, 2)`, **not** `HasColumnType` (F-02).
  - `ProductRepository` implements `GetBySkuAsync` with `FirstOrDefaultAsync`, and `ListAsync` with `AsNoTracking().OrderBy(p => p.Sku)`.
  - Move the seed from `Program.cs` to `DemoDataSeeder`. Same SKUs, names and prices. Insert only when `Products` is empty.
  - Hook it with `options.UseSeeding((ctx, _) => DemoDataSeeder.Seed((DemoDbContext)ctx)).UseAsyncSeeding((ctx, _, ct) => DemoDataSeeder.SeedAsync((DemoDbContext)ctx, ct))`. Both run during `EnsureCreated` now and during `Migrate` after Task 10.
- [ ] **Step 5: API adaptation.**
  - `ProductsController` (namespace `Demo.Api.Controllers`) gets `GET {sku}` → handler → `Ok(dto)` or `this.ToProblemResult(error)`. For now its constructor takes only the by-SKU handler. Task 13 adds the catalog handler to reach the shape in Appendix B.4.
  - `ErrorResults` per Appendix B.4.
  - `Program.cs`:
    - `builder.Services.AddProblemDetails()`
    - `app.UseExceptionHandler()` and `app.UseStatusCodePages()` before the other middleware
    - keep `EnsureCreated()` for now (removed in Task 10)
    - remove the inline seed.
- [ ] **Step 6: Verify.**
  - `dotnet test` is green.
  - Delete `src/Demo.Api/demo.db*`, run the app, and check:

| Request | Expected |
|---|---|
| `GET /api/products/SKU1` | `200 {"sku":"SKU1","name":"Name","price":103.30,"currency":"USD"}` |
| `GET /api/products/NOPE` | `404`, `application/problem+json`, `code: product.not_found`, no stack trace |
| `sqlite3`/Python `typeof(Price)` | `text` |

- [ ] **Step 7: Commit** `refactor(inventory): rich product model, use-case handlers and problem details`.

---

## Task 10: Persistence for exchange rates and migrations

**Files:**
- Infrastructure:
  - create `Persistence/Converters/UnixMillisecondsConverter.cs`, `Persistence/Configurations/{ExchangeRateConfiguration,ExchangeRateHistoryEntryConfiguration}.cs`, `Persistence/{ExchangeRateRepository,ExchangeRateHistoryRepository,DatabaseMigration}.cs`;
  - update `DemoDbContext.cs` (two new DbSets, `: IUnitOfWork`) and `DependencyInjection.cs`;
  - generate `Persistence/Migrations/*`.
- Api: `Demo.Api.csproj` gets `Microsoft.EntityFrameworkCore.Design` (`PrivateAssets=all`); `Program.cs` drops `EnsureCreated`.
- Root: `dotnet-tools.json`.
- New project `src/Demo.IntegrationTests`, added to the solution: references `Demo.Api` plus packages `Microsoft.AspNetCore.Mvc.Testing 10.0.14`, xunit, the runner, the SDK and coverlet. Add `<InternalsVisibleTo Include="Demo.IntegrationTests" />` to `Demo.Infrastructure.csproj`.
- Tests: `Persistence/SqliteTestDatabase.cs` (opens `Data Source=:memory:`, keeps the connection open, builds options with seeding, runs `MigrateAsync`), `Persistence/{MigrationAndSeedingTests,ExchangeRatePersistenceTests,ProductRepositoryTests}.cs`.

- [ ] **Step 1: Tool manifest.** `dotnet new tool-manifest`, then `dotnet tool install dotnet-ef --version 10.0.14`.
- [ ] **Step 2: Write the failing tests** (they compile once the DbSets exist).

| Test | Expected |
|---|---|
| `Migrate_CreatesSchemaAndSeedsProducts` | tables `Products`, `ExchangeRates`, `ExchangeRateHistory` exist; 2 products |
| `Migrate_RunTwice_SeedIsIdempotent` | still 2 products |
| `Price_IsStoredAsText` | raw SQL `typeof(Price)` = `text` |
| `ExchangeRate_RoundTripsFullPrecision` | save `0.92123456` → reload equals; `typeof(Rate)` = `text` |
| `ExchangeRate_TimestampsStoredAsUnixMilliseconds` | `typeof(PublishedAt)` = `integer`; reloads equal |
| `Repository_GetAsync_FindsByPair` | stored USD/EUR is found; USD/CAD → `null` |
| `Repository_AnyAsync` | `false` when empty, `true` after add + save |
| `History_DuplicatePublication_ViolatesUniqueIndex` | 2 entries with the same pair and `PublishedAt` → `DbUpdateException` |
| `UnitOfWork_SavesRateAndHistoryTogether` | one `SaveChangesAsync` persists both |
| `ProductRepository_ListAsync_OrderedBySku` | `[SKU1, SKU2]` |

- [ ] **Step 3: Implement** per Appendix B.1–B.2. `DemoDbContext.OnModelCreating` calls `modelBuilder.ApplyConfigurationsFromAssembly(typeof(DemoDbContext).Assembly)`. Register in `AddInfrastructure`:
  - `IUnitOfWork` → `sp.GetRequiredService<DemoDbContext>()` (scoped);
  - `IExchangeRateRepository` → `ExchangeRateRepository` (scoped);
  - `IExchangeRateHistoryRepository` → `ExchangeRateHistoryRepository` (scoped).
- [ ] **Step 4: Generate the migration.**

```
dotnet tool restore
dotnet ef migrations add InitialCreate --project src/Demo.Infrastructure --startup-project src/Demo.Api --output-dir Persistence/Migrations
```

Inspect the generated `CreateTable` calls:
- `Price` and `Rate` have `type: "TEXT"`;
- `PublishedAt`/`RetrievedAt` have `type: "INTEGER"`;
- a unique index exists on (`BaseCurrency`, `QuoteCurrency`, `PublishedAt`).

- [ ] **Step 5: `DatabaseMigration.MigrateDatabaseAsync`** (Appendix B.3). In `Program.cs`, replace `EnsureCreated` with `await app.Services.MigrateDatabaseAsync();` before `await app.RunAsync();`.
- [ ] **Step 6: Verify.**
  - `dotnet test` is green.
  - Delete `demo.db*`, run the app: the log shows the migration applied, and `GET /api/products/SKU1` → 200.
- [ ] **Step 7: Commit** `feat(persistence): store exchange rates with history and switch to migrations`.

---

## Task 11: Open Exchange Rates adapter

**Files:**
- Infrastructure: create `ExchangeRates/OpenExchangeRates/{OpenExchangeRatesOptions,OpenExchangeRatesResponses,OpenExchangeRatesClient}.cs`; update `DependencyInjection.cs`.
- Package `Microsoft.Extensions.Http.Resilience` `10.11.0` in CPM and in `Demo.Infrastructure.csproj`.
- Tests: `src/Demo.IntegrationTests/OpenExchangeRates/{StubHttpMessageHandler,OpenExchangeRatesClientTests}.cs`. The stub records the last request and returns a canned status code and body.

The client is built in tests as `new OpenExchangeRatesClient(new HttpClient(stub) { BaseAddress = new("https://oxr.test/api/") }, Options.Create(new OpenExchangeRatesOptions { AppId = "test-app-id" }), NullLogger<OpenExchangeRatesClient>.Instance)`.

- [ ] **Step 1: Write the failing tests.** Success body:

```json
{"disclaimer":"…","license":"…","timestamp":1761458400,"base":"USD","rates":{"EUR":0.921234,"CAD":1.401,"GBP":0.7512,"CHF":0.7987}}
```

| Test | Expected |
|---|---|
| `Request_UsesLatestEndpointWithSymbols` | path `/api/latest.json`, query `symbols=EUR,CAD,GBP,CHF`, **no** `base` parameter |
| `Request_SendsAppIdInHeaderNotUrl` | `Authorization: Token test-app-id`; URL does not contain `test-app-id` |
| `Response_MapsTimestampAndRates` | `PublishedAt == 2025-10-26T06:00:00Z`; EUR `0.921234m` exactly (decimal) |
| `Response_BaseMismatch_Throws` | `"base":"EUR"` → `ExchangeRateProviderException` |
| `Response_SkipsNonPositiveAndUnknownCodes` | `"CHF":0`, `"XX":1` → CHF and XX absent |
| `ErrorBody_MapsToProviderException` | 401 `{"error":true,"status":401,"message":"invalid_app_id","description":"Invalid App ID…"}` → exception message contains `invalid_app_id` and `401`, and does not contain the App ID |
| `MalformedJson_Throws` | `"not json"` → `ExchangeRateProviderException` with inner `JsonException` |
| `MissingAppId_ThrowsWithoutSendingRequest` | `AppId = null` → exception mentions `OpenExchangeRates:AppId`; stub request count `0` |

- [ ] **Steps 2–4:** red → implement per Appendix B.5 → green.
- [ ] **Step 5: Register.**

```csharp
services.AddOptions<OpenExchangeRatesOptions>()
    .Bind(configuration.GetSection(OpenExchangeRatesOptions.SectionName))
    .Validate(o => Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out _) && o.BaseUrl.EndsWith('/'), "OpenExchangeRates:BaseUrl must be an absolute URL ending with '/'.")
    .ValidateOnStart();
services.AddHttpClient<IExchangeRateProvider, OpenExchangeRatesClient>((sp, client) =>
        client.BaseAddress = new Uri(sp.GetRequiredService<IOptions<OpenExchangeRatesOptions>>().Value.BaseUrl))
    .AddStandardResilienceHandler();
```

`AppId` is deliberately not validated (ADR-0007). Add to `appsettings.json`: `"OpenExchangeRates": { "BaseUrl": "https://openexchangerates.org/api/" }`. **Never** add `AppId` there.

- [ ] **Step 6: Commit** `feat(exchange-rates): add open exchange rates adapter`.

---

## Task 12: Weekly Hangfire job and startup bootstrap

**Files:**
- Infrastructure: create `BackgroundJobs/{ExchangeRateSyncOptions,ExchangeRatesSyncJob,ExchangeRatesSyncScheduler}.cs`; update `DependencyInjection.cs`.
- Application: update `DependencyInjection.cs`.
- Api: `appsettings.json`; `Demo.Api.csproj` gets `UserSecretsId` via `dotnet user-secrets init --project src/Demo.Api`.
- Tests: `src/Demo.IntegrationTests/BackgroundJobs/{ExchangeRatesSyncSchedulerTests,ExchangeRatesSyncJobTests}.cs`.

- [ ] **Step 1: Write the failing tests.**
  - Scheduler tests build `var storage = new MemoryStorage();`, a `new RecurringJobManager(storage)` and a `new BackgroundJobClient(storage)`.
  - The scope factory comes from a `ServiceCollection` holding an `IExchangeRateRepository` fake.
  - Recurring jobs are read with `storage.GetConnection().GetRecurringJobs()`, enqueued jobs with `storage.GetMonitoringApi().EnqueuedCount("default")`.

| Test | Given | Expected |
|---|---|---|
| `Start_RegistersWeeklyRecurringJob` | defaults | job `exchange-rates-sync`, `Cron == "0 6 * * 1"`, `TimeZoneId == "UTC"` |
| `Start_EmptyStore_EnqueuesBootstrapRun` | `AnyAsync == false` | enqueued count `1` |
| `Start_RatesExist_DoesNotEnqueue` | `AnyAsync == true` | enqueued count `0` |
| `Start_BootstrapDisabled_DoesNotEnqueue` | `RunOnStartupWhenEmpty = false` | `0` |
| `Start_Disabled_RegistersNothing` | `Enabled = false` | no recurring jobs, `0` enqueued |
| `Start_InvalidCron_Throws` | `Cron = "not a cron"` | throws (fail-fast at startup) |
| `Job_FailureResult_Throws` | handler returns `exchange_rate.no_rates_applied` | `InvalidOperationException` with the error message, so Hangfire marks the run failed and retries |
| `Job_SuccessResult_Completes` | handler success | no exception |
| `Job_HasConcurrencyAndRetryAttributes` | reflection on `ExecuteAsync` | `DisableConcurrentExecution` present; `AutomaticRetry.Attempts == 3` |

- [ ] **Steps 2–4:** red → implement per Appendix B.6 → green.
- [ ] **Step 5: Register.**
  - `AddApplication`:
    - `ExchangeRatesOptions`: bind section `ExchangeRates`, add the validator as `IValidateOptions<ExchangeRatesOptions>` singleton, `ValidateOnStart()`;
    - `SupportedCurrencies` singleton from `IOptions<ExchangeRatesOptions>`;
    - `ICurrencyConversionReader` → `CurrencyConversionReader` (scoped);
    - `ICommandHandler<SynchronizeExchangeRatesCommand, Result<SynchronizationReport>>` → handler (scoped);
    - `TryAddSingleton(TimeProvider.System)`.
  - `AddInfrastructure`:
    - `ExchangeRateSyncOptions`: bind `ExchangeRates:Sync`, validate `TimeZoneInfo.TryFindSystemTimeZoneById(o.TimeZone, out _)`, `ValidateOnStart()`;
    - `services.AddHostedService<ExchangeRatesSyncScheduler>()`.
- [ ] **Step 6: `appsettings.json`:**

```json
"ExchangeRates": {
  "TargetCurrencies": [ "EUR", "CAD", "GBP", "CHF" ],
  "Sync": { "Enabled": true, "Cron": "0 6 * * 1", "TimeZone": "UTC", "RunOnStartupWhenEmpty": true }
}
```

- [ ] **Step 7: Verify without an App ID.**
  - Run the app. The Hangfire dashboard → *Recurring Jobs* shows `exchange-rates-sync` with cron `0 6 * * 1`.
  - The bootstrap run appears under *Failed* (after its retries) with the message about `OpenExchangeRates:AppId`.
  - The API still serves `GET /api/products/SKU1`.
- [ ] **Step 8: Commit** `feat(exchange-rates): schedule weekly sync with hangfire and bootstrap on empty store`.

---

## Task 13: Catalog endpoint with currency conversion

**Files:**
- Application: `DependencyInjection.cs` registers `IQueryHandler<GetProductCatalogQuery, Result<IReadOnlyList<ProductDto>>>`.
- Api: `ProductsController.cs` gets `GET` with `[FromQuery] string? currency`; `Program.cs` ends with `public partial class Program;`.
- Tests: `src/Demo.IntegrationTests/Api/{DemoApiFactory,ProductsEndpointTests}.cs`.

`DemoApiFactory : WebApplicationFactory<Program>, IAsyncLifetime`:
- environment `Testing`, so user-secrets are never loaded and a real App ID cannot leak into tests;
- `UseSetting("ConnectionStrings:Demo", $"Data Source={tempFile}")` and `UseSetting("ExchangeRates:Sync:Enabled", "false")`;
- a helper `SeedRateAsync(Currency quote, decimal rate)` writes through a scope's `DemoDbContext`;
- dispose calls `SqliteConnection.ClearAllPools()` and deletes the temp file.

- [ ] **Step 1: Write the failing tests.**

| Request | Setup | Expected |
|---|---|---|
| `GET /api/products` | none | `200`; body exactly `[{"sku":"SKU1","name":"Name","price":103.30,"currency":"USD"},{"sku":"SKU2","name":"Name","price":102.20,"currency":"USD"}]` |
| `GET /api/products?currency=EUR` | EUR `0.92` | `200`; prices `95.04` and `94.02`, `currency` `EUR` |
| `GET /api/products?currency=eur` | EUR `0.92` | same as above |
| `GET /api/products?currency=USD` | none | USD prices |
| `GET /api/products?currency=` | none | USD prices |
| `GET /api/products?currency=EURO` | none | `400`, `application/problem+json`, `code` `currency.invalid` |
| `GET /api/products?currency=JPY` | none | `400`, `code` `currency.unsupported`; `detail` contains `USD, EUR, CAD, GBP, CHF` |
| `GET /api/products?currency=CAD` | no CAD rate | `503`, `code` `exchange_rate.unavailable` |
| `GET /api/products/SKU1` | none | `200`, `currency` `USD` |
| `GET /api/products/NOPE` | none | `404`, `code` `product.not_found`; body has no `"stackTrace"`/`"exception"` |
| `GET /api/products` | `ConfigureTestServices` replaces `IProductRepository` with a fake that throws `InvalidOperationException("boom")` | `500`, `application/problem+json`, has `traceId`; body contains neither `boom` nor `at Demo.` (NFR-05) |

- [ ] **Steps 2–4:** red → implement the action per Appendix B.4 → green.
- [ ] **Step 5: Swagger.** Run the app in Development. `/swagger` shows `GET /api/products` with the `currency` parameter and responses 200/400/503, and `GET /api/products/{sku}` with 200/404.
- [ ] **Step 6: Commit** `feat(api): list product catalog with optional currency conversion`.

---

## Task 14: End-to-end test (sync → catalog)

**Files:** `src/Demo.IntegrationTests/EndToEnd/ExchangeRateSyncToCatalogTests.cs`.

The test uses `DemoApiFactory` with `ConfigureTestServices(s => s.AddSingleton<IExchangeRateProvider>(stub))`. The stub returns EUR `0.92`, CAD `1.40`, GBP `0.75`, CHF `0.80` at `t0`.

- [ ] **Step 1: Test** `Sync_ThenCatalogInEur_ReturnsConvertedPrices`.
  - Resolve `ExchangeRatesSyncJob` in a scope (`ActivatorUtilities.CreateInstance`) and call `ExecuteAsync(CancellationToken.None)`.
  - Then `GET /api/products?currency=EUR` → `95.04`/`94.02`.
  - `ExchangeRateHistory` has 4 rows.
  - Run the job again: still 4 history rows (idempotency, DR-05).
- [ ] **Step 2:** Run it. Expected: pass.
- [ ] **Step 3: Commit** `test: cover exchange rate sync through catalog conversion end to end`.

---

## Task 15: README

**Files:** `README.md`.

- [ ] **Step 1:** Keep the original sections and update them:
  - Prerequisites: add `dotnet tool restore` for EF tools.
  - Running: `dotnet build`/`dotnet test` from the root now work.
- [ ] **Step 2:** Add these sections:
  1. *Configuring Open Exchange Rates*:
     - `dotnet user-secrets set "OpenExchangeRates:AppId" "<id>" --project src/Demo.Api`, or the env var `OpenExchangeRates__AppId`;
     - without it, the API runs and only the sync fails.
  2. *Exchange-rate sync*: schedule (Monday 06:00 UTC, configurable), bootstrap on an empty database, manual trigger from `/hangfire` → *Recurring Jobs* → *Trigger now*.
  3. *Catalog endpoint*: `curl` examples for no currency, `EUR`, an unsupported code and the 503 case, plus the error matrix.
  4. *Upgrading from the original starter*: delete `src/Demo.Api/demo.db*` (created by `EnsureCreated`, incompatible with migrations).
  5. *Design documentation*: links to the spec, the ADRs, this plan and `Task1_CodeAnalysis.md`.
- [ ] **Step 3: Commit** `docs: document configuration, sync operation and catalog endpoint`.

---

## Task 16: Manual verification (real app)

- [ ] **Step 1:** Delete `demo.db*`. Run `cd src/Demo.Api && dotnet run --launch-profile http`. Check:

| Check | Expected |
|---|---|
| Startup log | migration applied; scheduler registered `exchange-rates-sync` |
| `/swagger` | both endpoints with documented responses |
| `/hangfire` → Recurring Jobs | `exchange-rates-sync`, `0 6 * * 1`, next execution on a Monday 06:00 UTC |
| `GET /api/products` | USD catalog with 2-decimal prices |
| `GET /api/products?currency=EUR` before any rate | `503` ProblemDetails |
| `GET /api/products/NOPE` | `404` ProblemDetails |

- [ ] **Step 2: Live smoke test** (only if an App ID is available).
  - Set it via user-secrets, restart, and let the bootstrap run or click *Trigger now*.
  - The job succeeds.
  - `GET ?currency=EUR|CAD|GBP|CHF` → `200` with converted prices.
  - Trigger again → the history row count is unchanged unless OXR published a newer timestamp.
- [ ] **Step 3:** Append the observed results (date, request, status, notable output) to the *Verification log* at the end of this plan.
- [ ] **Step 4: Commit** `docs: record manual verification results`.

---

## Task 17: Close-out

- [ ] **Step 1:** In `Task1_CodeAnalysis.md`, replace every `Planned: Task N` with `Fixed in <short-hash>`, using `git log --oneline`.
- [ ] **Step 2:** Walk the spec §14 traceability table. Every requirement must map to a test that exists by name. Fix any gap with a test.
- [ ] **Step 3: Final verification** from the root:
  - `dotnet build` → 0 warnings;
  - `dotnet test` → all passed (record the counts);
  - `dotnet list package --vulnerable --include-transitive` → none;
  - `git status` → clean apart from the Task 1 edit.
- [ ] **Step 4: Commit** `docs(task1): link findings to the commits that resolve them`.

---

## Appendix A: Domain and application contracts

These signatures are binding. Later tasks must use exactly these names.

### A.1 Shared kernel (`Demo.Domain.Common`)

```csharp
public sealed class DomainException(string message) : Exception(message);

public sealed record Currency
{
    public static readonly Currency USD = new("USD");
    public string Code { get; }
    private Currency(string code) => Code = code;
    public static Currency Create(string code);                       // throws DomainException when invalid
    public static bool TryParse(string? value, [NotNullWhen(true)] out Currency? currency); // trims, 3 ASCII letters, upper-cases
    public override string ToString() => Code;
}

public sealed record Money
{
    public const int MinorUnits = 2;
    public decimal Amount { get; }
    public Currency Currency { get; }
    public Money(decimal amount, Currency currency);                  // ArgumentNullException on null currency
    public Money RoundToMinorUnits();                                  // half away from zero, scale normalized to 2
}

public sealed record CurrencyConversion
{
    public Currency From { get; }
    public Currency To { get; }
    public decimal Rate { get; }
    public DateTimeOffset? AsOf { get; }
    public CurrencyConversion(Currency from, Currency to, decimal rate, DateTimeOffset? asOf); // rate <= 0 → DomainException
    public static CurrencyConversion Identity(Currency currency);      // rate 1, AsOf null
    public Money Convert(Money money);                                 // money.Currency != From → DomainException;
                                                                       // new Money(money.Amount * Rate, To).RoundToMinorUnits()
}
```

### A.2 Exchange Rates domain (`Demo.Domain.Modules.ExchangeRates`)

```csharp
public sealed record CurrencyPair
{
    public Currency Base { get; }
    public Currency Quote { get; }
    public CurrencyPair(Currency @base, Currency quote);               // equal → DomainException
    public override string ToString() => $"{Base}/{Quote}";
}

public sealed class ExchangeRate
{
    public Currency BaseCurrency { get; private set; }
    public Currency QuoteCurrency { get; private set; }
    public decimal Rate { get; private set; }
    public DateTimeOffset PublishedAt { get; private set; }
    public DateTimeOffset RetrievedAt { get; private set; }
    public CurrencyPair Pair => new(BaseCurrency, QuoteCurrency);
    private ExchangeRate() { }                                         // EF Core
    public static ExchangeRate Create(CurrencyPair pair, decimal rate, DateTimeOffset publishedAt, DateTimeOffset retrievedAt);
    /// <returns>false when publishedAt is not strictly newer (stale or duplicate observation); state unchanged.</returns>
    public bool Update(decimal rate, DateTimeOffset publishedAt, DateTimeOffset retrievedAt);
    public CurrencyConversion ToConversion();                          // AsOf = PublishedAt
}

public sealed class ExchangeRateHistoryEntry
{
    public long Id { get; private set; }
    public Currency BaseCurrency { get; private set; }
    public Currency QuoteCurrency { get; private set; }
    public decimal Rate { get; private set; }
    public DateTimeOffset PublishedAt { get; private set; }
    public DateTimeOffset RetrievedAt { get; private set; }
    private ExchangeRateHistoryEntry() { }                             // EF Core
    public static ExchangeRateHistoryEntry Record(ExchangeRate rate);
}

public interface IExchangeRateRepository
{
    Task<ExchangeRate?> GetAsync(CurrencyPair pair, CancellationToken cancellationToken);
    Task<bool> AnyAsync(CancellationToken cancellationToken);
    void Add(ExchangeRate exchangeRate);
}

public interface IExchangeRateHistoryRepository
{
    void Add(ExchangeRateHistoryEntry entry);
}
```

### A.3 Application building blocks (`Demo.Application.Common`)

```csharp
public enum ErrorType { Validation, NotFound, Unavailable }

public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Unavailable(string code, string message) => new(code, message, ErrorType.Unavailable);
}

public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public Error? Error { get; }
    public T Value { get; }                                            // throws InvalidOperationException on failure
    public static Result<T> Success(T value);
    public static Result<T> Failure(Error error);
    public static implicit operator Result<T>(Error error) => Failure(error);
}

public interface IQueryHandler<in TQuery, TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken);
}

public interface ICommandHandler<in TCommand, TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
```

### A.4 Exchange Rates application: configuration and contract (`Demo.Application.Modules.ExchangeRates`)

```csharp
public sealed class ExchangeRatesOptions
{
    public const string SectionName = "ExchangeRates";
    public string[] TargetCurrencies { get; init; } = [];   // defaults live in appsettings.json: binding appends to initialized collections
}

public sealed class ExchangeRatesOptionsValidator : IValidateOptions<ExchangeRatesOptions>; // non-empty, valid, distinct, not USD

public sealed class SupportedCurrencies
{
    public Currency Base { get; }                                      // Currency.USD (OXR free plan + stored product prices)
    public IReadOnlyList<Currency> Targets { get; }
    public IReadOnlyList<Currency> All { get; }                        // [Base, ..Targets]
    public SupportedCurrencies(IEnumerable<Currency> targets);
    public static SupportedCurrencies From(ExchangeRatesOptions options);
    public bool IsSupported(Currency currency);
}

public static class ExchangeRateErrors
{
    public static Error UnsupportedCurrency(Currency currency, IEnumerable<Currency> supported);   // Validation, "currency.unsupported"
    public static Error UnsupportedConversion(Currency from, Currency to, Currency baseCurrency);   // Validation, "conversion.unsupported"
    public static Error RateUnavailable(CurrencyPair pair);                                        // Unavailable, "exchange_rate.unavailable"
    public static Error NoRatesApplied(IEnumerable<Currency> missing);                             // Unavailable, "exchange_rate.no_rates_applied"
}

// Contracts/
public interface ICurrencyConversionReader
{
    IReadOnlyCollection<Currency> SupportedCurrencies { get; }
    Task<Result<CurrencyConversion>> GetConversionAsync(Currency from, Currency to, CancellationToken cancellationToken);
}
```

`CurrencyConversionReader.GetConversionAsync` evaluates in this order:
1. unsupported `from` or `to` → `UnsupportedCurrency`;
2. `from == to` → `Identity`, without touching storage;
3. `from != Base` → `UnsupportedConversion(from, to, Base)`;
4. repository lookup → `ToConversion()` or `RateUnavailable`.

### A.5 Exchange Rates application: port and sync use case

```csharp
// Ports/
public interface IExchangeRateProvider
{
    Task<LatestRates> GetLatestRatesAsync(Currency baseCurrency, IReadOnlyCollection<Currency> targets, CancellationToken cancellationToken);
}
public sealed record LatestRates(Currency Base, DateTimeOffset PublishedAt, IReadOnlyDictionary<Currency, decimal> Rates);
public sealed class ExchangeRateProviderException(string message, Exception? innerException = null) : Exception(message, innerException);

// SynchronizeExchangeRates/
public sealed record SynchronizeExchangeRatesCommand;
public sealed record SynchronizationReport(DateTimeOffset PublishedAt, IReadOnlyList<Currency> Applied, IReadOnlyList<Currency> Unchanged, IReadOnlyList<Currency> Missing);
public sealed class SynchronizeExchangeRatesCommandHandler : ICommandHandler<SynchronizeExchangeRatesCommand, Result<SynchronizationReport>>;
// ctor(IExchangeRateProvider, IExchangeRateRepository, IExchangeRateHistoryRepository, IUnitOfWork, SupportedCurrencies, TimeProvider, ILogger<…>)
```

Handler algorithm (spec §6.2):

```csharp
var latest = await _provider.GetLatestRatesAsync(_currencies.Base, _currencies.Targets, cancellationToken);
var retrievedAt = _timeProvider.GetUtcNow();
foreach (var target in _currencies.Targets)
{
    if (!latest.Rates.TryGetValue(target, out var value) || value <= 0) { missing.Add(target); continue; }   // + warning log
    var pair = new CurrencyPair(_currencies.Base, target);
    var rate = await _rates.GetAsync(pair, cancellationToken);
    if (rate is null) { rate = ExchangeRate.Create(pair, value, latest.PublishedAt, retrievedAt); _rates.Add(rate); }
    else if (!rate.Update(value, latest.PublishedAt, retrievedAt)) { unchanged.Add(target); continue; }
    _history.Add(ExchangeRateHistoryEntry.Record(rate));
    applied.Add(target);
}
if (applied.Count == 0 && unchanged.Count == 0) return ExchangeRateErrors.NoRatesApplied(missing);
if (applied.Count > 0) await _unitOfWork.SaveChangesAsync(cancellationToken);
// information log: published-at, applied / unchanged / missing
return Result<SynchronizationReport>.Success(new SynchronizationReport(latest.PublishedAt, applied, unchanged, missing));
```

### A.6 Inventory domain (`Demo.Domain.Modules.Inventory`)

```csharp
public sealed record Sku
{
    public const int MaxLength = 64;
    public string Value { get; }
    public static Sku Create(string value);                            // trims; empty or > 64 → DomainException
    public static bool TryCreate(string? value, [NotNullWhen(true)] out Sku? sku);
    public override string ToString() => Value;
}

public sealed class Product
{
    public const int NameMaxLength = 200;
    public static readonly Currency BaseCurrency = Currency.USD;      // stored prices are USD (spec A-03)
    public Sku Sku { get; private set; }
    public string Name { get; private set; }
    public Money Price { get; private set; }
    private Product() { }                                              // EF Core
    public static Product Create(Sku sku, string name, Money price);  // name non-empty ≤ 200 (trimmed); amount ≥ 0; currency == BaseCurrency
}

public interface IProductRepository
{
    Task<Product?> GetBySkuAsync(Sku sku, CancellationToken cancellationToken);
    Task<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken);   // ordered by SKU
}
```

### A.7 Inventory application (`Demo.Application.Modules.Inventory`)

```csharp
public sealed record ProductDto(string Sku, string Name, decimal Price, string Currency)
{
    public static ProductDto From(Product product, Money price) => new(product.Sku.Value, product.Name, price.Amount, price.Currency.Code);
}

public static class InventoryErrors
{
    public static Error ProductNotFound(string sku);                                       // NotFound, "product.not_found"
    public static Error InvalidCurrency(string value, IEnumerable<Currency> supported);    // Validation, "currency.invalid"
}

public sealed record GetProductBySkuQuery(string Sku);
public sealed class GetProductBySkuQueryHandler : IQueryHandler<GetProductBySkuQuery, Result<ProductDto>>;
// invalid or unknown SKU → ProductNotFound; found → ProductDto.From(p, p.Price.RoundToMinorUnits())

public sealed record GetProductCatalogQuery(string? Currency);
public sealed class GetProductCatalogQueryHandler : IQueryHandler<GetProductCatalogQuery, Result<IReadOnlyList<ProductDto>>>;
// ctor(IProductRepository, ICurrencyConversionReader)
```

`GetProductCatalogQueryHandler` evaluates in this order:
1. blank currency → `Product.BaseCurrency`; malformed → `InvalidCurrency(value, reader.SupportedCurrencies)`;
2. `reader.GetConversionAsync(Product.BaseCurrency, target)`; on failure, return its error;
3. `ListAsync`;
4. map each product through `ProductDto.From(p, conversion.Convert(p.Price))`.

### A.8 Messages

Messages are stable and name the offending value.

| Code | Message template |
|---|---|
| `currency.invalid` | `'{value}' is not a valid ISO 4217 currency code. Supported currencies: USD, EUR, CAD, GBP, CHF.` |
| `currency.unsupported` | `Currency '{code}' is not supported. Supported currencies: USD, EUR, CAD, GBP, CHF.` |
| `conversion.unsupported` | `Conversion from {from} to {to} is not supported; conversions must start from {base}.` |
| `exchange_rate.unavailable` | `No exchange rate is available yet for {pair}. Rates are synchronized weekly; try again later.` |
| `exchange_rate.no_rates_applied` | `The provider returned no usable rate for: {missing}.` |
| `product.not_found` | `No product with SKU '{sku}' exists.` |

---

## Appendix B: Infrastructure and API reference

### B.1 Mappings

```csharp
// ProductConfiguration
builder.ToTable("Products");
builder.HasKey(p => p.Sku);
builder.Property(p => p.Sku).HasConversion(new SkuConverter()).HasMaxLength(Sku.MaxLength);
builder.Property(p => p.Name).IsRequired().HasMaxLength(Product.NameMaxLength);
builder.ComplexProperty(p => p.Price, price =>
{
    price.Property(m => m.Amount).HasColumnName("Price").HasPrecision(18, 2);          // TEXT in SQLite: exact (F-02)
    price.Property(m => m.Currency).HasColumnName("PriceCurrency").HasConversion(new CurrencyConverter()).HasMaxLength(3);
});

// ExchangeRateConfiguration
builder.ToTable("ExchangeRates");
builder.HasKey(r => new { r.BaseCurrency, r.QuoteCurrency });
builder.Property(r => r.BaseCurrency).HasConversion(new CurrencyConverter()).HasMaxLength(3);
builder.Property(r => r.QuoteCurrency).HasConversion(new CurrencyConverter()).HasMaxLength(3);
builder.Property(r => r.Rate).HasPrecision(18, 8);
builder.Property(r => r.PublishedAt).HasConversion(new UnixMillisecondsConverter());
builder.Property(r => r.RetrievedAt).HasConversion(new UnixMillisecondsConverter());
builder.Ignore(r => r.Pair);

// ExchangeRateHistoryEntryConfiguration
builder.ToTable("ExchangeRateHistory");
builder.HasKey(e => e.Id);
builder.Property(e => e.Id).ValueGeneratedOnAdd();
builder.Property(e => e.BaseCurrency).HasConversion(new CurrencyConverter()).HasMaxLength(3);
builder.Property(e => e.QuoteCurrency).HasConversion(new CurrencyConverter()).HasMaxLength(3);
builder.Property(e => e.Rate).HasPrecision(18, 8);
builder.Property(e => e.PublishedAt).HasConversion(new UnixMillisecondsConverter());
builder.Property(e => e.RetrievedAt).HasConversion(new UnixMillisecondsConverter());
builder.HasIndex(e => new { e.BaseCurrency, e.QuoteCurrency, e.PublishedAt }).IsUnique();
```

Converters:
- `CurrencyConverter : ValueConverter<Currency, string>` (`c => c.Code`, `s => Currency.Create(s)`);
- `SkuConverter : ValueConverter<Sku, string>`;
- `UnixMillisecondsConverter : ValueConverter<DateTimeOffset, long>` (`d => d.ToUnixTimeMilliseconds()`, `l => DateTimeOffset.FromUnixTimeMilliseconds(l)`).

All of these were verified on EF Core 10.0.14 + SQLite with a throwaway probe: TEXT for decimals, INTEGER for timestamps, `ORDER BY`/comparisons translated, and the complex type materialized through its constructor.

### B.2 Repositories

- `ExchangeRateRepository.GetAsync` → `_dbContext.ExchangeRates.FirstOrDefaultAsync(r => r.BaseCurrency == pair.Base && r.QuoteCurrency == pair.Quote, cancellationToken)`.
- `AnyAsync` → `_dbContext.ExchangeRates.AnyAsync(cancellationToken)`.
- `Add` → `_dbContext.ExchangeRates.Add(rate)`.
- `ExchangeRateHistoryRepository.Add` → `_dbContext.ExchangeRateHistory.Add(entry)`.
- No repository calls `SaveChanges`.

### B.3 Database startup

```csharp
public static class DatabaseMigration
{
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DemoDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);      // also runs UseAsyncSeeding
    }
}
```

### B.4 API

```csharp
[ApiController]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    // ctor(IQueryHandler<GetProductCatalogQuery, Result<IReadOnlyList<ProductDto>>>, IQueryHandler<GetProductBySkuQuery, Result<ProductDto>>)

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProductDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> GetCatalogAsync([FromQuery] string? currency, CancellationToken cancellationToken)
    {
        var result = await _getProductCatalog.HandleAsync(new GetProductCatalogQuery(currency), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error!);
    }

    [HttpGet("{sku}")]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetBySkuAsync(string sku, CancellationToken cancellationToken)
    {
        var result = await _getProductBySku.HandleAsync(new GetProductBySkuQuery(sku), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error!);
    }
}

public static class ErrorResults
{
    public static IActionResult ToProblemResult(this ControllerBase controller, Error error)
    {
        var (status, title) = error.Type switch
        {
            ErrorType.Validation => (StatusCodes.Status400BadRequest, "Invalid request"),
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Resource not found"),
            ErrorType.Unavailable => (StatusCodes.Status503ServiceUnavailable, "Service unavailable"),
            _ => throw new ArgumentOutOfRangeException(nameof(error))
        };
        var problem = controller.ProblemDetailsFactory.CreateProblemDetails(controller.HttpContext, status, title, detail: error.Message);
        problem.Extensions["code"] = error.Code;
        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }
}
```

`Program.cs` (final):
1. `AddControllers`, `AddEndpointsApiExplorer`, `AddSwaggerGen`, `AddProblemDetails`, `AddApplication(builder.Configuration)`, `AddInfrastructure(builder.Configuration)`;
2. `UseExceptionHandler()`, `UseStatusCodePages()`, Swagger in Development, `UseHttpsRedirection()`, `UseHangfireDashboard()`, `MapControllers()`;
3. `await app.Services.MigrateDatabaseAsync()`, `await app.RunAsync()`;
4. `public partial class Program;`.

### B.5 Open Exchange Rates client

```csharp
internal sealed class OpenExchangeRatesClient : IExchangeRateProvider
{
    // ctor(HttpClient, IOptions<OpenExchangeRatesOptions>, ILogger<OpenExchangeRatesClient>)
    public async Task<LatestRates> GetLatestRatesAsync(Currency baseCurrency, IReadOnlyCollection<Currency> targets, CancellationToken cancellationToken)
    {
        var appId = _options.Value.AppId;
        if (string.IsNullOrWhiteSpace(appId))
            throw new ExchangeRateProviderException("Open Exchange Rates App ID is not configured. Set 'OpenExchangeRates:AppId' with user-secrets or the 'OpenExchangeRates__AppId' environment variable.");

        // Codes are validated ISO letters, so the comma-separated list needs no escaping. 'base' is never sent (paid feature; USD is the default).
        using var request = new HttpRequestMessage(HttpMethod.Get, $"latest.json?symbols={string.Join(',', targets.Select(t => t.Code))}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Token", appId);   // keeps the secret out of URLs and logs

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw await CreateProviderExceptionAsync(response, cancellationToken);

        LatestRatesResponse? payload;
        try { payload = await response.Content.ReadFromJsonAsync<LatestRatesResponse>(cancellationToken); }
        catch (JsonException ex) { throw new ExchangeRateProviderException("Open Exchange Rates returned a malformed response.", ex); }

        // validate: payload not null, Rates not null, base parses and equals baseCurrency → else ExchangeRateProviderException
        // map: each (code, value) with Currency.TryParse(code) && value > 0 → dictionary; others → warning log
        // PublishedAt = DateTimeOffset.FromUnixTimeSeconds(payload.Timestamp)
    }
}

internal sealed record LatestRatesResponse(long Timestamp, string? Base, Dictionary<string, decimal>? Rates);   // decimal, never double
internal sealed record ErrorResponse(bool Error, int Status, string? Message, string? Description);
```

`CreateProviderExceptionAsync` tries to read `ErrorResponse`. Its message is `Open Exchange Rates request failed with HTTP {status} ({message}): {description}`, or `… HTTP {status}.` when the body is not an OXR error.

### B.6 Background jobs

```csharp
public sealed class ExchangeRateSyncOptions
{
    public const string SectionName = "ExchangeRates:Sync";
    public bool Enabled { get; init; } = true;
    public string Cron { get; init; } = "0 6 * * 1";
    public string TimeZone { get; init; } = "UTC";
    public bool RunOnStartupWhenEmpty { get; init; } = true;
}

public sealed class ExchangeRatesSyncJob
{
    public const string RecurringJobId = "exchange-rates-sync";
    // ctor(ICommandHandler<SynchronizeExchangeRatesCommand, Result<SynchronizationReport>>)

    [JobDisplayName("Synchronize exchange rates")]
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await _handler.HandleAsync(new SynchronizeExchangeRatesCommand(), cancellationToken);
        if (!result.IsSuccess) throw new InvalidOperationException(result.Error!.Message);   // Hangfire records and retries
    }
}

internal sealed class ExchangeRatesSyncScheduler : IHostedService
{
    // ctor(IRecurringJobManager, IBackgroundJobClient, IServiceScopeFactory, IOptions<ExchangeRateSyncOptions>, ILogger<…>)
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;
        if (!options.Enabled) return;
        _recurringJobs.AddOrUpdate<ExchangeRatesSyncJob>(
            ExchangeRatesSyncJob.RecurringJobId,
            job => job.ExecuteAsync(CancellationToken.None),          // Hangfire substitutes its own cancellation token
            options.Cron,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZone) });
        if (!options.RunOnStartupWhenEmpty) return;
        await using var scope = _scopeFactory.CreateAsyncScope();
        var rates = scope.ServiceProvider.GetRequiredService<IExchangeRateRepository>();
        if (!await rates.AnyAsync(cancellationToken))
            _backgroundJobs.Enqueue<ExchangeRatesSyncJob>(job => job.ExecuteAsync(CancellationToken.None));
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

---

## Requirement → task coverage

| Requirement | Task(s) |
|---|---|
| FR-T1-01 | 1, 17 |
| FR-T2-01 | 11, 16 |
| FR-T2-02, FR-T2-03 | 7, 8, 11 |
| FR-T2-04 | 12, 16 |
| FR-T2-05, FR-T2-06 | 5, 8, 10, 14 |
| FR-T3-01 … FR-T3-05 | 4, 9, 13, 14 |
| DR-01, DR-02, DR-03 | 7, 9, 13 |
| DR-04 | 4, 13 |
| DR-05, DR-06, DR-07 | 5, 8, 10, 14 |
| DR-08, DR-11, DR-12 | 11, 12 |
| DR-09 | 11, 12, 13, 15 |
| DR-10 | 9, 13 |
| NFR-01, NFR-02, NFR-07 | 2, 17 |
| NFR-03 | 4, 9, 10, 11 |
| NFR-04 | 3, 9 |
| NFR-05 | 9, 13 |
| NFR-06 | 11, 13, 14 |

---

## Verification log

Entries are added during Task 16 (manual checks against the running app) and Task 17 (final command outputs). Each entry records the date, the exact request or command, and the observed result.
