using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

internal sealed class PoolingStringTextConverter : CsvConverter<char, string>
{
    public override bool HandleNull => true;

    public static PoolingStringTextConverter SharedInstance { get; } = new(CsvTextOptions.Default);

    private readonly StringPool _stringPool;
    private readonly string? _null;

    public PoolingStringTextConverter(CsvTextOptions options)
    {
        _stringPool = options.StringPool ?? StringPool.Shared;
        _null = options.NullTokens.TryGetValue(typeof(string), out var value) ? value : null;
    }

    public override bool TryFormat(Span<char> destination, string value, out int charsWritten)
    {
        if (value is null)
            return _null.AsSpan().TryWriteTo(destination, out charsWritten);

        return value.AsSpan().TryWriteTo(destination, out charsWritten);
    }

    public override bool TryParse(ReadOnlySpan<char> source, [MaybeNullWhen(false)] out string value)
    {
        value = _stringPool.GetOrAdd(source);
        return true;
    }
}
