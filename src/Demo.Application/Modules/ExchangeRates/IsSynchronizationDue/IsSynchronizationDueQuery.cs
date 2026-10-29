namespace Demo.Application.Modules.ExchangeRates.IsSynchronizationDue;

/// <param name="StaleAfter">Age after which a stored rate counts as missed; normally one schedule period.</param>
public sealed record IsSynchronizationDueQuery(TimeSpan StaleAfter);
