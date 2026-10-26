namespace Demo.Domain.Modules.ExchangeRates;

/// <summary>Append-only log of every rate observation that changed the current state.</summary>
public interface IExchangeRateHistoryRepository
{
    void Add(ExchangeRateHistoryEntry entry);
}
