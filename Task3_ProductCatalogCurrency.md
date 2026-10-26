# Task 3: Product Catalog with Currency Conversion

**⚠️ Dependency:** This task requires the successful completion of **Task 1** (Exchange Rate Synchronization).

## 📖 Context
Now that our system fetches and stores currency exchange rates every week, we want to expose this value to our customers. When a customer browses the product catalog, they should be able to see the prices in their preferred local currency.

## 🎯 The Goal
Your task is to create or extend an API endpoint that retrieves the entire product catalog, applying a dynamic currency conversion based on the rates stored in the database.

## ✅ Requirements

1. **API Endpoint Extension:**
   * Create an endpoint to list all products (e.g., `GET /api/products`).
   * Introduce an optional query parameter for the target currency (e.g., `?currency=EUR`).

2. **Currency Conversion Logic:**
   * If **no currency** is specified, the endpoint should return the product prices in their base currency (**USD**).
   * If a **supported currency** (EUR, CAD, GBP, or CHF) is specified, the endpoint should calculate and return the price converted to that currency using the *latest available exchange rate* from the database.
   * Ensure the returned JSON payload clearly indicates the currency of the price (e.g., `{ "sku": "...", "price": 95.50, "currency": "EUR" }`).

