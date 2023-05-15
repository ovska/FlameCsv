using System.Collections.Concurrent;
using FlameCsv.Extensions;

namespace FlameCsv.Converters.Text;

/// <summary>
/// Parser for non-flags enums.
/// </summary>
internal sealed class EnumTextConverter<TEnum> : CsvConverter<char, TEnum>
    where TEnum : struct, Enum
{
    private readonly bool _allowUndefinedValues;
    private readonly bool _ignoreCase;

    private readonly ConcurrentDictionary<TEnum, string> _writeCache;

    public EnumTextConverter(CsvOptions<char> options)
    {
        _allowUndefinedValues = options.AllowUndefinedEnumValues;
        _ignoreCase = options.IgnoreEnumCase;
        _writeCache = new();
    }

    public override bool TryFormat(Span<char> destination, TEnum value, out int charsWritten)
    {
        if (!_writeCache.TryGetValue(value, out string? name))
        {
            name = value.ToString();

            if (_writeCache.Count <= 64)
            {
                _writeCache.TryAdd(value, name);
            }
        }

        return name.AsSpan().TryWriteTo(destination, out charsWritten);
    }

    public override bool TryParse(ReadOnlySpan<char> source, out TEnum value)
    {
        return Enum.TryParse(source, _ignoreCase, out value) && (_allowUndefinedValues || Enum.IsDefined(value));
    }
}
