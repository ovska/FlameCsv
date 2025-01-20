using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace FlameCsv.Converters;

internal sealed class SpanUtf8FormattableConverter<TValue> : CsvConverter<byte, TValue>
    where TValue : IUtf8SpanFormattable, ISpanParsable<TValue>
{
    private readonly string? _format;
    private readonly IFormatProvider? _provider;

    public SpanUtf8FormattableConverter(CsvOptions<byte> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _format = options.GetFormat(typeof(TValue));
        _provider = options.GetFormatProvider(typeof(TValue));
    }

    public override bool TryFormat(Span<byte> destination, TValue value, out int charsWritten)
    {
        return value.TryFormat(destination, out charsWritten, _format, _provider);
    }

    public override bool TryParse(ReadOnlySpan<byte> source, [MaybeNullWhen(false)] out TValue value)
    {
        if (source.Length == 0) return TValue.TryParse([], _provider, out value);

        int len = Encoding.UTF8.GetMaxCharCount(source.Length);

        scoped Span<char> buffer;
        char[]? toReturn = null;

        if (Token<char>.CanStackalloc(len))
        {
            buffer = stackalloc char[len];
        }
        else
        {
            buffer = toReturn = ArrayPool<char>.Shared.Rent(len);
        }

        int written = Encoding.UTF8.GetChars(source, buffer);

        bool result = TValue.TryParse(buffer[..written], _provider, out value);

        if (toReturn is not null)
        {
            ArrayPool<char>.Shared.Return(toReturn);
        }

        return result;
    }
}
