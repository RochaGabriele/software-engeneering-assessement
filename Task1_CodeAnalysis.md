# Task 1: Code Analysis & Refactoring Identification

## Objective
Analyze the existing codebase, run and test the application, and identify areas that you believe should be improved, refactored, or fixed.

## Instructions
1. **Explore the Code:** Review the current structure, following the flow from the API layer down to the Domain and Infrastructure layers.
2. **Run the Application:** Start the application, interact with the Swagger UI, check the Hangfire dashboard, and observe the application's behavior.
3. **Identify Improvements:** Look for issues related to clean architecture principles, design patterns, error handling, performance, or overall code quality.
4. **Document Findings:** You **do not** need to implement these changes right now. Simply document your findings directly in this file.

---

## 📝 Your Findings

*Please list the situations you consider should be altered below.*

### How the analysis was done

All locations refer to the original starter code, commit `a456096`.

- Read the code end to end, from `ProductsController` through `InventoryModule` and `ProductRepository` to `DemoDbContext`, plus the composition in `Program.cs`.
- Built the solution and ran the API with the `http` profile.
- Called the endpoints with Swagger and `curl`, and opened the Hangfire dashboard.
- Inspected the generated SQLite database (`demo.db`) directly.
- Ran `dotnet list package --vulnerable --include-transitive`.

**Severity:**
- **High:** wrong behavior, data integrity, or a security exposure.
- **Medium:** a maintainability, operability, or design risk that will cost later.
- **Low:** hygiene and consistency.

**Scope rule for fixes.** As instructed, this task only documents. Findings that block Tasks 2–3, or sit in code those tasks must change, are fixed as part of that work, and their *Status* column links the commit. Everything else stays a recommendation. Rationale and design: [`docs/specs`](docs/specs/2026-10-26-exchange-rates-catalog-design.md) and [`docs/adr`](docs/adr/README.md).

### Summary

| ID | Severity | Area | Finding | Status |
|---|---|---|---|---|
| F-01 | High | Error handling | Unknown SKU returns 500 with a stack trace | Fixed in `fb9b9d4` |
| F-02 | High | Data integrity | Prices are stored as binary floating point (REAL) in SQLite | Fixed in `fb9b9d4` |
| F-03 | High | Data / schema | `EnsureCreated()` cannot evolve the schema | Fixed in `b583a92` (legacy database detection in `01d42ea`) |
| F-04 | High | Security | Three packages with known high-severity vulnerabilities | Fixed in `2414249` |
| F-05 | High | DDD | Anemic `Product` model with no invariants and primitive obsession | Fixed in `fb9b9d4` |
| F-06 | Medium | Architecture | The API returns the domain entity | Fixed in `fb9b9d4` |
| F-07 | Medium | Architecture | Composition root hard-codes infrastructure details | Fixed in `d07a50a` |
| F-08 | Medium | Design (SOLID) | `InventoryModule` is a pass-through facade with dead code | Fixed in `fb9b9d4` |
| F-09 | Medium | Architecture | `IMessagePublisher` lives in Domain, is unused and silently drops messages | Recommended |
| F-10 | Medium | Operability | Seeding is inline, synchronous and mixed with bootstrapping | Fixed in `fb9b9d4` |
| F-11 | Medium | Async / API design | No `CancellationToken` propagation; `Async` naming not followed | Fixed in `fb9b9d4` |
| F-12 | Medium | Tooling | README build and test commands fail as documented | Fixed in `2414249` |
| F-13 | Medium | Testing | No tests at all | Fixed in `8303098`…`df54a8c` (181 tests) |
| F-14 | Medium | Security | Hangfire dashboard has no explicit authorization | Recommended |
| F-15 | Medium | Operability | Hangfire MemoryStorage loses state and does not support several instances | Recommended (restart-safe scheduling in `a2460ac`, missed-run catch-up in `14e882d`) |
| F-16 | Medium | Security | The API has no authentication or authorization | Recommended |
| F-17 | Low | Naming | Controller namespace and class names are inconsistent | Fixed in `fb9b9d4` |
| F-18 | Low | API documentation | Swagger documents only an untyped `200` | Fixed in `fb9b9d4`, `0eba70e` |
| F-19 | Low | Dependencies | `Microsoft.AspNetCore.OpenApi` is referenced but unused | Fixed in `2414249` |
| F-20 | Low | Dependency injection | Lifetimes are inconsistent with the scoped `DbContext` | Fixed in `d07a50a` |
| F-21 | Low | Build | Build settings and package versions are duplicated per project | Fixed in `2414249` |
| F-22 | Low | Repository hygiene | `.DS_Store` is committed and there is no `.gitattributes` | Fixed in `2414249` |
| F-23 | Low | HTTP | HTTPS redirection is misconfigured for the HTTP profile and there is no HSTS | Recommended |
| F-24 | Low | Observability | No health checks, metrics or tracing | Recommended |
| F-25 | Low | Scalability | SQLite and an unpaginated catalog are not production choices | Recommended |
| F-26 | Low | Ubiquitous language | The "Inventory" module actually holds the product catalog | Recommended |

---

### F-01: Unknown SKU returns 500 with a stack trace (High)

- **Location:** `src/Demo.Infrastructure/Persistence/ProductRepository.cs:17`, `src/Demo.Api/Controllers/ProductsController.cs:20-25`
- **Observation:** `GET /api/products/NOPE` responds `500` with the full `System.InvalidOperationException: Sequence contains no elements` stack trace, which names internal types and file paths. `FirstAsync` throws for a missing row, and nothing translates "not found" into an HTTP outcome. Outside Development the client would get an empty 500.
- **Impact:** A normal client mistake looks like a server failure. Monitoring gets polluted with false 500s, and in Development internal structure is disclosed. There is no machine-readable error contract.
- **Recommendation:**
  - Return `null` from the repository (`FirstOrDefaultAsync`).
  - Model "not found" as an expected outcome in the application layer.
  - Map it to `404`.
  - Emit RFC 9457 ProblemDetails for every error, including unhandled exceptions (`AddProblemDetails`, `UseExceptionHandler`).
- **Status:** Fixed in `fb9b9d4`

### F-02: Prices are stored as binary floating point in SQLite (High)

- **Location:** `src/Demo.Infrastructure/Persistence/DemoDbContext.cs:23`
- **Observation:** `HasColumnType("decimal(18,2)")` creates the column as `"Price" decimal(18,2)`. In SQLite that type name gets **NUMERIC affinity**, so the engine converts the text value EF Core writes into a REAL. On the generated database, `SELECT typeof(Price) FROM Products` returns `real` for every row. The `(18,2)` declaration is not enforced either.
- **Impact:** The C# `decimal` guarantee is lost at rest. Values beyond ~15 significant digits are rounded through IEEE-754 doubles, and any SQL-side arithmetic or comparison works on approximations. It is invisible in casual testing because small prices round-trip "nicely".
- **Recommendation:** Declare precision with `HasPrecision(18, 2)`. EF Core then stores the value as `TEXT`, which is exact, and the precision metadata stays correct for other providers.
- **Status:** Fixed in `fb9b9d4`

### F-03: `EnsureCreated()` cannot evolve the schema (High)

- **Location:** `src/Demo.Api/Program.cs:51`
- **Observation:** The schema is created with `Database.EnsureCreated()`, which does nothing if the database file already exists.
- **Impact:** Any schema change, such as the tables needed for exchange rates, is never applied to an existing database, and the app fails at runtime. There is no versioned history of schema changes.
- **Recommendation:** Use EF Core Migrations, applied at startup for this demo or by a deployment step in production, with a reviewed `InitialCreate` migration.
- **Status:** Fixed in `b583a92` (a database created by the original `EnsureCreated` is detected with an actionable message in `01d42ea`)

### F-04: Packages with known high-severity vulnerabilities (High)

- **Location:** `src/Demo.Api/Demo.Api.csproj`, `src/Demo.Infrastructure/Demo.Infrastructure.csproj` (all transitive)
- **Observation:** Every build emits 8 × `NU1903`:

| Package | Resolved | Advisory | Fixed in |
|---|---|---|---|
| `Microsoft.OpenApi` (via Swashbuckle / `Microsoft.AspNetCore.OpenApi`) | 2.4.1 | GHSA-v5pm-xwqc-g5wc | 2.7.5 |
| `Newtonsoft.Json` (via Hangfire) | 11.0.1 | GHSA-5crp-9r3c-p9vr | 13.0.1 |
| `SQLitePCLRaw.lib.e_sqlite3` (via EF Core SQLite) | 2.1.11 | GHSA-2m69-gcr7-jv3q | > 2.1.11 |

- **Impact:** Known-vulnerable code ships in the application, and the warnings train developers to ignore build output.
- **Recommendation:** Upgrade the direct dependencies that pull patched versions (EF Core 10.0.14, Swashbuckle 10.3.1) and pin `Newtonsoft.Json` with Central Package Management transitive pinning. Keep `dotnet list package --vulnerable` in CI.
- **Status:** Fixed in `2414249`

### F-05: Anemic `Product` model with no invariants and primitive obsession (High)

- **Location:** `src/Demo.Domain/Modules/Inventory/Product.cs:3-8`
- **Observation:**
  - All properties have public setters, and there is no constructor logic, so an empty SKU, an empty name or a negative price can all be created.
  - The SKU is a bare `string`.
  - The price is a bare `decimal` that does not say which currency it is in.
- **Impact:** Business rules can only live outside the entity, scattered and duplicated, or nowhere. Currency is implicit, which is exactly the information Task 3 needs to make explicit.
- **Recommendation:**
  - Make `Product` an aggregate root with private setters and a factory that enforces its invariants.
  - Introduce value objects: `Sku`, plus `Money` and `Currency` in a small shared kernel.
- **Status:** Fixed in `fb9b9d4`

### F-06: The API returns the domain entity (Medium)

- **Location:** `src/Demo.Api/Controllers/ProductsController.cs:20-25`
- **Observation:** `GetProduct` returns the `Product` entity, which is serialized straight to JSON.
- **Impact:** The public HTTP contract is coupled to the domain model. Renaming or encapsulating a domain property silently breaks clients, and internal state can leak as the entity grows.
- **Recommendation:** Return dedicated response DTOs mapped in the application layer.
- **Status:** Fixed in `fb9b9d4`

### F-07: Composition root hard-codes infrastructure details (Medium)

- **Location:** `src/Demo.Api/Program.cs:17-29`
- **Observation:** `Program.cs` registers each concrete infrastructure type itself and hard-codes `"Data Source=demo.db"`.
- **Impact:** The API project must know every implementation detail of every layer, and each new feature edits `Program.cs`. The connection string cannot change per environment without a code change.
- **Recommendation:** Expose `AddApplication()` and `AddInfrastructure()` from each layer and keep `Program.cs` as a thin composition root. Read the connection string from configuration.
- **Status:** Fixed in `d07a50a`

### F-08: `InventoryModule` is a pass-through facade with dead code (Medium)

- **Location:** `src/Demo.Application/Modules/InventoryModule.cs:6-29`
- **Observation:** The module only forwards to the repository. The injected `_logger` (line 13) is never used. Every new use case would add one more method and one more dependency to the same class.
- **Impact:** Violates SRP and ISP as the module grows, and consumers depend on operations they do not use.
- **Recommendation:** One handler per use case (query/command plus handler) behind small interfaces, without a mediator library.
- **Status:** Fixed in `fb9b9d4`

### F-09: `IMessagePublisher` lives in Domain, is unused and silently drops messages (Medium)

- **Location:** `src/Demo.Domain/Common/Services/IMessagePublisher.cs`, `src/Demo.Infrastructure/Messaging/FakeMessagePublisher.cs`, `src/Demo.Api/Program.cs:23`
- **Observation:**
  - A messaging port sits in the Domain layer, although messaging is an application/integration concern.
  - Nothing calls it.
  - `FakeMessagePublisher` is registered for **every** environment and only logs a warning, so any future caller would lose messages in production without failing.
  - There is no outbox, so a publish after a database commit could be lost or duplicated.
- **Impact:** Misplaced abstraction and a silent data-loss trap waiting for its first caller.
- **Recommendation:**
  - Move the port to the Application layer.
  - Register the fake only in Development/tests.
  - Introduce a transactional outbox when the first real integration event appears.
- **Status:** Recommended

### F-10: Seeding is inline, synchronous and mixed with bootstrapping (Medium)

- **Location:** `src/Demo.Api/Program.cs:47-75`
- **Observation:** Seed data is created inside `Program.cs` with synchronous `Any()` and `SaveChanges()`, in the same block as schema creation. Both seeded products are named "Name".
- **Impact:** Startup logic grows untestable, it blocks threads, and the seed data does not look like a catalog.
- **Recommendation:** Use EF Core's `UseSeeding`/`UseAsyncSeeding`, which run with migrations and are idempotent. Use realistic product names. The current values are kept in this delivery for compatibility with the original behavior.
- **Status:** Fixed in `fb9b9d4`

### F-11: No `CancellationToken` propagation; `Async` naming not followed (Medium)

- **Location:** `src/Demo.Domain/Modules/Inventory/IProductRepository.cs:5`, `src/Demo.Application/Modules/InventoryModule.cs:25`, `src/Demo.Api/Controllers/ProductsController.cs:20`
- **Observation:** Asynchronous methods take no `CancellationToken` and are named without the `Async` suffix (`GetProduct`).
- **Impact:** Aborted HTTP requests and application shutdown cannot cancel database work, and the naming hides that the methods are asynchronous.
- **Recommendation:** Accept and pass a `CancellationToken` through every layer, and use the `Async` suffix.
- **Status:** Fixed in `fb9b9d4`

### F-12: README build and test commands fail as documented (Medium)

- **Location:** `README.md` ("Running the Application", "Running Tests"), `src/DemoSolution.slnx`
- **Observation:** The README says to run `dotnet build` and `dotnet test` from the root directory. Both fail with `MSB1003: Specify a project or solution file`, because the solution file lives in `src/`.
- **Impact:** The first experience of every new developer and reviewer is a failure.
- **Recommendation:** Move the solution file to the repository root, so the documented commands work as written.
- **Status:** Fixed in `2414249`

### F-13: No tests at all (Medium)

- **Location:** `src/Demo.UnitTests/UnitTest1.cs:5-9`, `src/Demo.UnitTests/Demo.UnitTests.csproj:18`
- **Observation:** The only test is an empty placeholder, and the test project references only the Application layer. There are no integration tests for persistence or HTTP.
- **Impact:** No behavior is protected against regression, and refactoring is unsafe.
- **Recommendation:**
  - Unit tests for domain and application.
  - Integration tests for persistence (real SQLite) and for the HTTP API (`WebApplicationFactory`).
  - No live external calls in automated tests.
- **Status:** Fixed in `8303098`…`df54a8c` (181 tests)

### F-14: Hangfire dashboard has no explicit authorization (Medium)

- **Location:** `src/Demo.Api/Program.cs:43`
- **Observation:** `UseHangfireDashboard()` relies on the default `LocalRequestsOnlyAuthorizationFilter`. Behind a reverse proxy on the same host, and unless forwarded headers are processed, every request appears to come from the loopback address and is let in.
- **Impact:** Anyone reaching the proxy could trigger, delete or inspect background jobs.
- **Recommendation:** Configure `DashboardOptions.Authorization` with a policy tied to the application's authentication. Consider making the dashboard read-only (`IsReadOnlyFunc`) in production.
- **Status:** Recommended

### F-15: Hangfire MemoryStorage loses state and does not support several instances (Medium)

- **Location:** `src/Demo.Infrastructure/HangfireSetup.cs:16`
- **Observation:** Job storage is in-process memory.
- **Impact:**
  - Recurring job definitions, queues and history disappear on every restart.
  - With several instances, each one keeps its own storage, so every recurring job runs once per instance.
  - Distributed locks such as `DisableConcurrentExecution` do not work across instances.
- **Recommendation:** Use persistent shared storage (SQL Server, PostgreSQL or Redis) before going beyond a single instance. In this delivery, the recurring job is registered idempotently on every startup, so a restart cannot lose the schedule, and a run missed while the app was down is caught up on the next startup.
- **Status:** Recommended (restart-safe scheduling in `a2460ac`, missed-run catch-up in `14e882d`)

### F-16: The API has no authentication or authorization (Medium)

- **Location:** `src/Demo.Api/Program.cs`
- **Observation:** No authentication scheme or authorization policy is configured.
- **Impact:** Acceptable for a public read-only catalog, but any future write endpoint, and the Hangfire dashboard (F-14), would be open.
- **Recommendation:** Add an authentication scheme (for example JWT bearer) and a default authorization policy. Mark public endpoints explicitly with `[AllowAnonymous]`.
- **Status:** Recommended

### F-17: Controller namespace and class names are inconsistent (Low)

- **Location:** `src/Demo.Api/Controllers/ProductsController.cs:4,8`
- **Observation:** The namespace is `Ecommerce.Api.Controllers` inside the `Demo.Api` project, and the class `ProductController` lives in `ProductsController.cs`.
- **Impact:** Code is harder to find and looks like copy-paste leftovers.
- **Recommendation:** Rename to `Demo.Api.Controllers.ProductsController`.
- **Status:** Fixed in `fb9b9d4`

### F-18: Swagger documents only an untyped `200` (Low)

- **Location:** `src/Demo.Api/Controllers/ProductsController.cs:19-20`
- **Observation:** The generated OpenAPI document lists `200 OK` with no schema and no error responses.
- **Impact:** Clients cannot generate types or learn the error cases from the contract.
- **Recommendation:** Return typed results and declare `ProducesResponseType` for success and every ProblemDetails status.
- **Status:** Fixed in `fb9b9d4`, `0eba70e`

### F-19: `Microsoft.AspNetCore.OpenApi` is referenced but unused (Low)

- **Location:** `src/Demo.Api/Demo.Api.csproj:10`
- **Observation:** There is no `AddOpenApi()`/`MapOpenApi()` call. Swashbuckle generates the document.
- **Impact:** Dead dependency surface, which contributes to F-04.
- **Recommendation:** Remove the package, or migrate to it and drop Swashbuckle. Not both.
- **Status:** Fixed in `2414249`

### F-20: Lifetimes are inconsistent with the scoped `DbContext` (Low)

- **Location:** `src/Demo.Api/Program.cs:22,23,26`
- **Observation:** The repository and the module are registered as `Transient`, while `DemoDbContext` is `Scoped`.
- **Impact:** It works because a transient resolved within a scope receives the scoped context. However, the intent ("one unit of work per request") is not expressed, and new instances are created needlessly.
- **Recommendation:** Register request-bound services as `Scoped`.
- **Status:** Fixed in `d07a50a`

### F-21: Build settings and package versions are duplicated per project (Low)

- **Location:** every `src/**/*.csproj`
- **Observation:** `TargetFramework`, `Nullable` and `ImplicitUsings` are repeated in each project, and package versions are set per project (the framework packages already drift: `Microsoft.AspNetCore.OpenApi` 10.0.4 versus EF Core and `Microsoft.Extensions.Logging.Abstractions` 10.0.5).
- **Impact:** Version drift and inconsistent compiler settings.
- **Recommendation:** Use `Directory.Build.props` for shared properties and `Directory.Packages.props` (Central Package Management) for versions.
- **Status:** Fixed in `2414249`

### F-22: `.DS_Store` is committed and there is no `.gitattributes` (Low)

- **Location:** repository root
- **Observation:** A macOS Finder metadata file is versioned, and line endings are not normalized. Git warns "LF will be replaced by CRLF" for every file on Windows.
- **Impact:** Noise in diffs and in reviews.
- **Recommendation:** Ignore `.DS_Store` and add `.gitattributes` with `* text=auto`.
- **Status:** Fixed in `2414249`

### F-23: HTTPS redirection is misconfigured for the HTTP profile and there is no HSTS (Low)

- **Location:** `src/Demo.Api/Program.cs:40`
- **Observation:** Under the `http` launch profile, the log shows `warn: HttpsRedirectionMiddleware[3] Failed to determine the https port for redirect.` and no redirect happens. HSTS is not enabled.
- **Impact:** Misleading configuration, with no transport-security policy for production.
- **Recommendation:** Configure the HTTPS port (or rely on the reverse proxy), and add `UseHsts()` outside Development.
- **Status:** Recommended

### F-24: No health checks, metrics or tracing (Low)

- **Location:** `src/Demo.Api/Program.cs`
- **Observation:** There are no `/health` endpoints and no OpenTelemetry instrumentation.
- **Impact:** Orchestrators cannot probe liveness or readiness, and production issues are hard to diagnose.
- **Recommendation:** `AddHealthChecks()` with a database check, and OpenTelemetry for ASP.NET Core, HttpClient and EF Core.
- **Status:** Recommended

### F-25: SQLite and an unpaginated catalog are not production choices (Low)

- **Location:** `src/Demo.Api/Program.cs:18-19`; the catalog endpoint required by Task 3
- **Observation:** SQLite is file-based with a single writer. Task 3 explicitly asks for the **entire** catalog in one response.
- **Impact:** Fine for this assessment. In production it would not scale with concurrent writers or a large catalog.
- **Recommendation:** Use a server database in production. Introduce pagination (or a cursor) once the catalog grows, as an additive, versioned API change.
- **Status:** Recommended

### F-26: The "Inventory" module actually holds the product catalog (Low)

- **Location:** `src/Demo.Domain/Modules/Inventory/`, `src/Demo.Application/Modules/InventoryModule.cs`
- **Observation:** The module contains SKU, name and price, but no stock concept. Task 3 calls it the "product catalog".
- **Impact:** The ubiquitous language diverges between code and business.
- **Recommendation:** Rename to `Catalog` when a real inventory (stock) concept appears. The name is kept now to avoid churn unrelated to the tasks.
- **Status:** Recommended

### Prioritized recommendations (not implemented in this delivery)

1. **F-14 (Hangfire dashboard authorization):** the only open path to *changing* system state. Close it before any non-local deployment.
2. **F-15 (persistent Hangfire storage):** a prerequisite for running more than one instance. Without it, recurring jobs duplicate and locks stop working.
3. **F-16 (API authentication):** required as soon as any non-public endpoint exists.
4. **F-09 (messaging port and outbox):** fix the placement now, and add the outbox together with the first integration event.
5. **F-24 (health checks and telemetry):** cheap to add and needed for any orchestrated deployment.
6. **F-25 (production database and pagination):** plan these with the first production capacity estimate.
7. **F-23 (HTTPS/HSTS):** configure together with the hosting topology (reverse proxy or direct TLS).
8. **F-26 (rename to Catalog):** do it when the inventory concept arrives, so the rename carries meaning.
