using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

internal sealed class PoolingStringTextConverter : CsvConverter<char, string>
{
    private readonly StringPool _stringPool;

    public PoolingStringTextConverter(StringPool stringPool)
    {
        ArgumentNullException.ThrowIfNull(stringPool);
        _stringPool = stringPool;
    }

    public override bool TryFormat(Span<char> destination, string value, out int charsWritten)
    {
        return value.AsSpan().TryWriteTo(destination, out charsWritten);
    }

    public override bool TryParse(ReadOnlySpan<char> source, [MaybeNullWhen(false)] out string value)
    {
        value = _stringPool.GetOrAdd(source);
        return true;
    }
}
