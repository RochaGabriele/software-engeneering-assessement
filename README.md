# Technical Assessment

Welcome to the technical assessment! This repository contains a starter project designed for evaluating candidates.

## 📌 Project Overview
This project is a simplified backend API. It provides the foundation for you to demonstrate your system design skills, coding standards, understanding of clean architecture, and ability to tackle complex backend scenarios using modern .NET technologies.

## 🏗️ Architecture & Design
The codebase is structured following **Clean Architecture** and **Domain-Driven Design (DDD)** principles to separate concerns and ensure maintainability.

* **`Demo.Api`**: The entry point of the application. It contains ASP.NET Core Controllers, Swagger configuration, and Dependency Injection setups.
* **`Demo.Application`**: Contains the business orchestration logic (e.g., `InventoryModule`). This layer coordinates the workflow.
* **`Demo.Domain`**: The core of the system. It contains domain entities (like `Product`), interfaces (e.g., `IProductRepository`), and core business logic.
* **`Demo.Infrastructure`**: Implementation details. This layer handles data persistence using Entity Framework Core (SQLite), external services, and background job processing with Hangfire.
* **`Demo.UnitTests`**: The test suite project validating core behaviors.

## 🛠️ Tech Stack
* **Framework:** .NET 10.0
* **Persistence:** Entity Framework Core with SQLite (used for simplicity so no external database setup is required)
* **Background Processing:** Hangfire (configured with Memory Storage)
* **Testing:** xUnit

## 🚀 Getting Started

### Prerequisites
* [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
* An IDE of your choice (Visual Studio 2022, JetBrains Rider, or VS Code)

### Running the Application

1. **Restore dependencies and build the solution:**
   ```bash
   dotnet build
   ```

2. **Run the API:**
   Navigate to the API folder and start the application. The SQLite database will be automatically created and seeded with initial data on startup.
   ```bash
   cd src/Demo.Api
   dotnet run
   ```

3. **Useful Endpoints:**
   * **Swagger UI:** `https://localhost:<port>/swagger` (or `http://localhost:<port>/swagger`) - Explore and test the API endpoints.
   * **Hangfire Dashboard:** `https://localhost:<port>/hangfire` - Monitor background jobs.

### Running Tests
To execute the unit tests, run the following command from the root directory:
```bash
dotnet test
```

---

## 📝 Candidate Instructions

Your assessment is broken down into specific tasks. Please start by reading:

👉 **[Task 1: Code Analysis & Refactoring Identification](Task1_CodeAnalysis.md)**

👉 **[Task 2: Exchange Rate Synchronization](Task2_ExchangeRates.md)**

👉 **[Task 3: Product Catalog with Currency Conversion](Task3_ProductCatalogCurrency.md)**

Please complete the tasks assigned above. You are expected to treat this codebase as if it were a real production system.

**Note:** There is no single "correct" answer for these tasks. What matters most is your ability to explain your reasoning and justify the technical decisions you make. Additionally, there are no restrictions on the tools or resources you use to complete this assessment, feel free to use whatever helps you deliver your best work.

**Good luck!**