using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Demo.Infrastructure.Persistence.Converters;

/// <summary>
/// Stores a <see cref="DateTimeOffset"/> as UNIX milliseconds (UTC). The SQLite provider cannot translate
/// ordering or comparisons on DateTimeOffset; an INTEGER column keeps both translatable.
/// </summary>
internal sealed class UnixMillisecondsConverter() : ValueConverter<DateTimeOffset, long>(
    value => value.ToUnixTimeMilliseconds(),
    milliseconds => DateTimeOffset.FromUnixTimeMilliseconds(milliseconds));
