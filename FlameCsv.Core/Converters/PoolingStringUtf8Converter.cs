using System.Diagnostics.CodeAnalysis;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

internal sealed class PoolingStringUtf8Converter : CsvConverter<byte, string>
{
    private readonly StringPool _stringPool;

    public PoolingStringUtf8Converter(StringPool stringPool)
    {
        ArgumentNullException.ThrowIfNull(stringPool);
        _stringPool = stringPool;
    }

    public override bool TryParse(ReadOnlySpan<byte> span, [MaybeNullWhen(false)] out string value)
    {
        int maxLength = Encoding.UTF8.GetMaxCharCount(span.Length);

        if (Token<char>.CanStackalloc(maxLength))
        {
            Span<char> buffer = stackalloc char[maxLength];
            int written = Encoding.UTF8.GetChars(span, buffer);
            value = _stringPool.GetOrAdd(buffer[..written]);
        }
        else
        {
            value = _stringPool.GetOrAdd(span, Encoding.UTF8);
        }

        return true;
    }

    public override bool TryFormat(Span<byte> buffer, string value, out int charsWritten)
    {
        return value.AsSpan().TryWriteUtf8To(buffer, out charsWritten);
    }
}
