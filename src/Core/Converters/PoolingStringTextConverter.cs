using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

internal sealed class PoolingStringTextConverter : CsvConverter<char, string>
{
    public static PoolingStringTextConverter SharedInstance { get; } = new();

    public StringPool Pool { get; }

    public PoolingStringTextConverter()
    {
        Pool = StringPool.Shared;
    }

    public PoolingStringTextConverter(StringPool? pool)
    {
        Pool = pool ?? StringPool.Shared;
    }

    public override bool TryFormat(Span<char> destination, string value, out int charsWritten)
    {
        // use String.TryCopyTo to avoid conversion to span
        if (value.TryCopyTo(destination))
        {
            charsWritten = value.Length;
            return true;
        }

        charsWritten = 0;
        return false;
    }

    public override bool TryParse(ReadOnlySpan<char> source, [MaybeNullWhen(false)] out string value)
    {
        value = Pool.GetOrAdd(source);
        return true;
    }
}
