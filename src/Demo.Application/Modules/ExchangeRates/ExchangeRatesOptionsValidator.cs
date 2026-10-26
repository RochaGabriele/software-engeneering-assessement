using Demo.Domain.Common;
using Microsoft.Extensions.Options;

namespace Demo.Application.Modules.ExchangeRates;

/// <summary>Fails startup on a misconfigured currency list instead of failing later inside the job or the API.</summary>
public sealed class ExchangeRatesOptionsValidator : IValidateOptions<ExchangeRatesOptions>
{
    private const string Key = $"{ExchangeRatesOptions.SectionName}:{nameof(ExchangeRatesOptions.TargetCurrencies)}";

    public ValidateOptionsResult Validate(string? name, ExchangeRatesOptions options)
    {
        var failures = new List<string>();
        var seen = new HashSet<Currency>();

        if (options.TargetCurrencies.Length == 0)
        {
            failures.Add($"{Key} must list at least one currency.");
        }

        foreach (var value in options.TargetCurrencies)
        {
            if (!Currency.TryParse(value, out var currency))
            {
                failures.Add($"{Key} contains '{value}', which is not a valid ISO 4217 code.");
            }
            else if (currency == Currency.USD)
            {
                failures.Add($"{Key} must not contain the base currency {Currency.USD}.");
            }
            else if (!seen.Add(currency))
            {
                failures.Add($"{Key} lists {currency} more than once.");
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
