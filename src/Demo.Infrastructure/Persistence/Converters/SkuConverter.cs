using Demo.Domain.Modules.Inventory;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Demo.Infrastructure.Persistence.Converters;

internal sealed class SkuConverter() : ValueConverter<Sku, string>(
    sku => sku.Value,
    value => Sku.Create(value));
