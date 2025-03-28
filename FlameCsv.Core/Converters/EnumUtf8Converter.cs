using System.Buffers;
using System.Collections.Frozen;
using System.Text;
using System.Text.Unicode;
using FlameCsv.Utilities;

namespace FlameCsv.Converters;

/// <summary>
/// The default converter for non-flags enums.
/// </summary>
internal sealed class EnumUtf8Converter<TEnum> : CsvConverter<byte, TEnum> where TEnum : struct, Enum
{
    private readonly bool _allowUndefinedValues;
    private readonly bool _ignoreCase;
    private readonly string? _format;
    private readonly FrozenDictionary<StringLike, TEnum>.AlternateLookup<ReadOnlySpan<byte>> _values;
    private readonly FrozenDictionary<TEnum, byte[]>? _names;

    /// <summary>
    /// Initializes a new enum converter.
    /// </summary>
    public EnumUtf8Converter(CsvOptions<byte> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _allowUndefinedValues = (options.EnumOptions & CsvEnumOptions.AllowUndefinedValues) != 0;
        _ignoreCase = (options.EnumOptions & CsvEnumOptions.IgnoreCase) != 0;
        _format = options.GetFormat(typeof(TEnum), options.EnumFormat);

        bool useEnumMember = (options.EnumOptions & CsvEnumOptions.UseEnumMemberAttribute) != 0;

        if (!EnumMemberCache<TEnum>.HasFlagsAttribute)
        {
            _values = EnumCacheUtf8<TEnum>.GetReadValues(_ignoreCase, useEnumMember);

            if (EnumMemberCache<TEnum>.IsSupported(_format))
            {
                _names = EnumCacheUtf8<TEnum>.GetWriteValues(_format, useEnumMember);
            }
        }
    }

    /// <inheritdoc/>
    public override bool TryParse(ReadOnlySpan<byte> source, out TEnum value)
    {
        if (EnumMemberCache<byte, TEnum>.TryGetFast(source, out value))
        {
            return true;
        }

        if (_values.Dictionary is not null && _values.TryGetValue(source, out value))
        {
            return true;
        }

        int maxLength = Encoding.UTF8.GetMaxCharCount(source.Length);
        char[]? toReturn = null;

        scoped Span<char> chars;

        if (Token<char>.CanStackalloc(maxLength) ||
            Token<char>.CanStackalloc(maxLength = Encoding.UTF8.GetCharCount(source)))
        {
            chars = stackalloc char[maxLength];
        }
        else
        {
            chars = toReturn = ArrayPool<char>.Shared.Rent(maxLength);
        }

        int written = Encoding.UTF8.GetChars(source, chars);

        bool result =
            Enum.TryParse(chars[..written], _ignoreCase, out value) &&
            (
                _allowUndefinedValues ||
                (_names?.ContainsKey(value) == true) ||
                Enum.IsDefined(value)
            );

        if (toReturn is not null)
        {
            ArrayPool<char>.Shared.Return(toReturn);
        }

        return result;
    }

    /// <inheritdoc/>
    public override bool TryFormat(Span<byte> destination, TEnum value, out int charsWritten)
    {
        if (_names is not null && _names.TryGetValue(value, out byte[]? name))
        {
            if (destination.Length >= name.Length)
            {
                name.CopyTo(destination);
                charsWritten = name.Length;
                return true;
            }

            charsWritten = 0;
            return false;
        }

        // Enum doesn't support Utf8 formatting directly
        Utf8.TryWriteInterpolatedStringHandler handler = new(
            literalLength: 0,
            formattedCount: 1,
            destination: destination,
            shouldAppend: out bool shouldAppend);

        if (shouldAppend)
        {
            // the handler needs to be constructed by hand so we can pass in the dynamic format
            handler.AppendFormatted(value, _format);
            return Utf8.TryWrite(destination, ref handler, out charsWritten);
        }

        charsWritten = 0;
        return false;
    }
}
