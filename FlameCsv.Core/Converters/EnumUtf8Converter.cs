using System.Buffers;
using System.Text;
using System.Text.Unicode;

namespace FlameCsv.Converters;

public sealed class EnumUtf8Converter<TEnum>
    : CsvConverter<byte, TEnum> where TEnum : struct, Enum
{
    private readonly bool _allowUndefinedValues;
    private readonly bool _ignoreCase;

    public EnumUtf8Converter(CsvOptions<byte> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _allowUndefinedValues = options.AllowUndefinedEnumValues;
        _ignoreCase = options.IgnoreEnumCase;
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
            int exactLength = Encoding.UTF8.GetCharCount(source);

            if (Token<char>.CanStackalloc(exactLength))
            {
                chars = stackalloc char[exactLength];
            }
            else
            {
                chars = toReturn = ArrayPool<char>.Shared.Rent(maxLength);
            }
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
        return Utf8.TryWrite(destination, $"{value}", out charsWritten);
    }
}
