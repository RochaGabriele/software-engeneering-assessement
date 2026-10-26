namespace Demo.Application.Modules.ExchangeRates.Ports;

public sealed class ExchangeRateProviderException(string message, Exception? innerException = null)
    : Exception(message, innerException);
