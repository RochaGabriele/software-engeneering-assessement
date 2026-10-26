# Task 2: Exchange Rate Synchronization

## 📖 Context
As part of our continuous effort to support an international customer base, the platform needs to keep track of currency exchange rates. This will eventually allow us to display our product catalog with accurate local pricing based on current conversion rates.

## 🎯 The Goal
Your task is to implement a background process that automatically fetches the latest currency exchange rates and persists them in the database.

## ✅ Requirements

1. **Data Source Integration:**
   * Integrate with the Open Exchange Rates API.
   * You can sign up for a free developer account to get your API Key here: [Open Exchange Rates Free Plan](https://openexchangerates.org/signup/free)

2. **Currency Scope:**
   * Fetch the exchange rates for the following currencies: **EUR**, **CAD**, **GBP**, and **CHF**.
   * Use **USD** as the base currency.

3. **Background Scheduling:**
   * The synchronization process must run automatically **every Monday**.
   * Please leverage **Hangfire** (which is already configured) to schedule this recurring job.

4. **Persistence:**
   * Design the necessary changes to store these exchange rates.
   * Save the fetched data to the SQLite database. Ensure that the latest rates are either updated and historically logged.

