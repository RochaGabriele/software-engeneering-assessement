using Demo.Domain.Common;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Demo.Infrastructure.Persistence.Converters;

internal sealed class CurrencyConverter() : ValueConverter<Currency, string>(
    currency => currency.Code,
    code => Currency.Create(code));
