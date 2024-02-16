using System.Diagnostics.CodeAnalysis;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;

namespace FlameCsv.Converters;

public sealed class DelegatingUtf8Converter<TValue> : CsvConverter<byte, TValue>
{
    public override bool HandleNull => _converter.HandleNull;

    private readonly CsvConverter<char, TValue> _converter;

    public DelegatingUtf8Converter(CsvConverter<char, TValue> converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        _converter = converter;
    }

    public override bool TryFormat(Span<byte> destination, TValue value, out int charsWritten)
    {
        int maxLength = Encoding.UTF8.GetMaxByteCount(destination.Length);

        if (Token<char>.CanStackalloc(maxLength))
        {
            return TryFormatImpl(destination, stackalloc char[maxLength], value, out charsWritten);
        }
        else
        {
            using var owner = SpanOwner<char>.Allocate(maxLength);
            return TryFormatImpl(destination, owner.Span, value, out charsWritten);
        }
    }

    private bool TryFormatImpl(Span<byte> destination, Span<char> charDestination, TValue value, out int charsWritten)
    {
        if (_converter.TryFormat(charDestination, value, out charsWritten))
        {
            charsWritten = Encoding.UTF8.GetBytes(charDestination, destination);
            return true;
        }

        return false;
    }

    public override bool TryParse(ReadOnlySpan<byte> source, [MaybeNullWhen(false)] out TValue value)
    {
        if (source.IsEmpty)
        {
            return _converter.TryParse([], out value);
        }

        int maxLength = Encoding.UTF8.GetMaxCharCount(source.Length);

        if (Token<char>.CanStackalloc(maxLength))
        {
            return TryParseImpl(source, stackalloc char[maxLength], out value);
        }
        else
        {
            using var owner = SpanOwner<char>.Allocate(maxLength);
            return TryParseImpl(source, owner.Span, out value);
        }
    }

    private bool TryParseImpl(ReadOnlySpan<byte> source, Span<char> buffer, [MaybeNullWhen(false)] out TValue value)
    {
        int written = Encoding.UTF8.GetChars(source, buffer);
        return _converter.TryParse(buffer.Slice(0, written), out value);
    }
}
