using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

internal sealed class PoolingStringTextConverter : CsvConverter<char, string>
{
    public static PoolingStringTextConverter SharedInstance { get; } = new(CsvOptions<char>.Default);

    private readonly StringPool _stringPool;

    public PoolingStringTextConverter(CsvOptions<char> options) : this(options?.StringPool)
    {
    }

    public PoolingStringTextConverter(StringPool? pool)
    {
        _stringPool = pool ?? StringPool.Shared;
    }

    public override bool TryFormat(Span<char> destination, string value, out int charsWritten)
    {
        return value.AsSpan().TryCopyTo(destination, out charsWritten);
    }

    public override bool TryParse(ReadOnlySpan<char> source, [MaybeNullWhen(false)] out string value)
    {
        value = _stringPool.GetOrAdd(source);
        return true;
    }
}
