using Demo.Domain.Modules.ExchangeRates;

namespace Demo.UnitTests.Fakes;

public sealed class InMemoryExchangeRateHistoryRepository : IExchangeRateHistoryRepository
{
    private readonly List<ExchangeRateHistoryEntry> _entries = [];

    public IReadOnlyList<ExchangeRateHistoryEntry> Entries => _entries;

    public void Add(ExchangeRateHistoryEntry entry) => _entries.Add(entry);
}
