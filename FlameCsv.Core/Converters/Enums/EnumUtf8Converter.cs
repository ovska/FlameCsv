using System.Buffers;
using System.Collections.Frozen;
using System.Text;
using System.Text.Unicode;
using FlameCsv.Extensions;
using FlameCsv.Utilities;

namespace FlameCsv.Converters.Enums;

/// <summary>
/// The default converter for non-flags enums.
/// </summary>
internal sealed class EnumUtf8Converter<TEnum> : CsvConverter<byte, TEnum> where TEnum : struct, Enum
{
    private readonly bool _allowUndefinedValues;
    private readonly bool _ignoreCase;
    private readonly string? _format;
    private readonly EnumParseStrategy<byte, TEnum> _parseStrategy;
    private readonly EnumFormatStrategy<byte, TEnum> _formatStrategy;

    /// <summary>
    /// Initializes a new enum converter.
    /// </summary>
    public EnumUtf8Converter(CsvOptions<byte> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _allowUndefinedValues = options.AllowUndefinedEnumValues;
        _ignoreCase = options.IgnoreEnumCase;
        _format = options.GetFormat(typeof(TEnum), options.EnumFormat);

        var formatStrategy = new FormatStrategy(_format);

        if (EnumMemberCache<TEnum>.IsFlagsFormat(_format))
        {
            EnumMemberCache<TEnum>.EnsureFlagsAttribute();
            _formatStrategy = new CsvEnumFlagsUtf8FormatStrategy<TEnum>(options, formatStrategy);
        }
        else
        {
            _formatStrategy = formatStrategy;
        }

        var parseStrategy = new ParseStrategy(_allowUndefinedValues, _ignoreCase);
        _parseStrategy = EnumMemberCache<TEnum>.HasFlagsAttribute
            ? new CsvEnumFlagsParseStrategy<byte, TEnum>(options, parseStrategy)
            : parseStrategy;
    }

    /// <inheritdoc/>
    public override bool TryParse(ReadOnlySpan<byte> source, out TEnum value)
    {
        if (_parseStrategy.TryParse(source, out value))
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
            (_allowUndefinedValues || EnumCacheUtf8<TEnum>.IsDefinedCore(value));

        if (toReturn is not null)
        {
            ArrayPool<char>.Shared.Return(toReturn);
        }

        return result;
    }

    /// <inheritdoc/>
    public override bool TryFormat(Span<byte> destination, TEnum value, out int charsWritten)
    {
        OperationStatus status = _formatStrategy.TryFormat(destination, value, out charsWritten);

        if (status is OperationStatus.Done) return true;
        if (status is OperationStatus.DestinationTooSmall) return false;

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

    private sealed class ParseStrategy : EnumParseStrategy<byte, TEnum>
    {
        private readonly bool _allowUndefinedValues;

        private readonly FrozenDictionary<StringLike, TEnum>.AlternateLookup<ReadOnlySpan<byte>> _values;

        public ParseStrategy(bool allowUndefinedValues, bool ignoreCase)
        {
            _allowUndefinedValues = allowUndefinedValues;
            _values = EnumCacheUtf8<TEnum>.GetReadValues(ignoreCase);
        }

        public override bool TryParse(ReadOnlySpan<byte> source, out TEnum value)
        {
            if (source.IsEmpty)
            {
                value = default;
                return false;
            }

            if (EnumExtensions.CanParseNumber<byte, TEnum>(source) && EnumExtensions.TryParseNumber(source, out value))
            {
                return _allowUndefinedValues || EnumCacheUtf8<TEnum>.IsDefinedCore(value);
            }

            if (_values.Dictionary is not null && _values.TryGetValue(source, out value))
            {
                return true;
            }

            value = default;
            return false;
        }
    }

    private sealed class FormatStrategy(string? format) : EnumFormatStrategy<byte, TEnum>
    {
        private readonly FrozenDictionary<TEnum, byte[]>? _names = EnumCacheUtf8<TEnum>.GetWriteValues(format);

        public override OperationStatus TryFormat(Span<byte> destination, TEnum value, out int charsWritten)
        {
            if (_names is not null && _names.TryGetValue(value, out byte[]? name))
            {
                if (destination.Length >= name.Length)
                {
                    name.CopyTo(destination);
                    charsWritten = name.Length;
                    return OperationStatus.Done;
                }

                charsWritten = 0;
                return OperationStatus.DestinationTooSmall;
            }

            charsWritten = 0;
            return OperationStatus.InvalidData;
        }
    }
}
