using System.Buffers;
using System.Text;
using System.Text.Unicode;

namespace FlameCsv.Converters;

public sealed class EnumUtf8Converter<TEnum>
    : CsvConverter<byte, TEnum> where TEnum : struct, Enum
{
    private readonly bool _allowUndefinedValues;
    private readonly bool _ignoreCase;
    private readonly string? _format;
    private readonly IFormatProvider? _formatProvider;

    public EnumUtf8Converter(CsvOptions<byte> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _allowUndefinedValues = options.AllowUndefinedEnumValues;
        _ignoreCase = options.IgnoreEnumCase;
        _formatProvider = options.GetFormatProvider(typeof(TEnum));
        _format = options.GetFormat(typeof(TEnum), options.EnumFormat);
    }

    public override bool TryParse(ReadOnlySpan<byte> source, out TEnum value)
    {
        int maxLength = Encoding.UTF8.GetMaxCharCount(source.Length);
        char[]? toReturn = null;

        scoped Span<char> chars;

        if (Token<char>.CanStackalloc(maxLength))
        {
            chars = stackalloc char[maxLength];
        }
        else
        {
            chars = toReturn = ArrayPool<char>.Shared.Rent(maxLength);
        }

        int written = Encoding.UTF8.GetChars(source, chars);

        bool result = Enum.TryParse(chars[..written], _ignoreCase, out value)
            && (_allowUndefinedValues || Enum.IsDefined(value));

        if (toReturn is not null)
            ArrayPool<char>.Shared.Return(toReturn);

        return result;
    }

    public override bool TryFormat(Span<byte> destination, TEnum value, out int charsWritten)
    {
        Utf8.TryWriteInterpolatedStringHandler handler = new(
            literalLength: 0,
            formattedCount: 1,
            destination: destination,
            provider: _formatProvider,
            shouldAppend: out bool shouldAppend);

        if (shouldAppend)
        {
            handler.AppendFormatted(value, _format);
            return Utf8.TryWrite(destination, ref handler, out charsWritten);
        }

        charsWritten = 0;
        return false;
    }
}
