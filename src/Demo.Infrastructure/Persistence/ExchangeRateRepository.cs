using Demo.Domain.Modules.ExchangeRates;
using Microsoft.EntityFrameworkCore;

namespace Demo.Infrastructure.Persistence;

public sealed class ExchangeRateRepository : IExchangeRateRepository
{
    private readonly DemoDbContext _dbContext;

    public ExchangeRateRepository(DemoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<ExchangeRate?> GetAsync(CurrencyPair pair, CancellationToken cancellationToken) =>
        _dbContext.ExchangeRates.FirstOrDefaultAsync(
            rate => rate.BaseCurrency == pair.Base && rate.QuoteCurrency == pair.Quote,
            cancellationToken);

    public async Task<IReadOnlyList<ExchangeRate>> ListAsync(CancellationToken cancellationToken) =>
        await _dbContext.ExchangeRates.AsNoTracking().ToListAsync(cancellationToken);

    public void Add(ExchangeRate exchangeRate) => _dbContext.ExchangeRates.Add(exchangeRate);
}
