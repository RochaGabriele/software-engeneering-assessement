using Demo.Domain.Modules.ExchangeRates;

namespace Demo.Infrastructure.Persistence;

public sealed class ExchangeRateHistoryRepository : IExchangeRateHistoryRepository
{
    private readonly DemoDbContext _dbContext;

    public ExchangeRateHistoryRepository(DemoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Add(ExchangeRateHistoryEntry entry) => _dbContext.ExchangeRateHistory.Add(entry);
}
