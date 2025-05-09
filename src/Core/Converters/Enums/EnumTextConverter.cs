using System.Buffers;
using System.Collections.Frozen;
using FlameCsv.Reflection;

namespace FlameCsv.Converters.Enums;

/// <summary>
/// The default converter for non-flags enums.
/// </summary>
internal sealed class EnumTextConverter<TEnum> : CsvConverter<char, TEnum>
    where TEnum : struct, Enum
{
    private readonly bool _allowUndefinedValues;
    private readonly bool _ignoreCase;
    private readonly string? _format;
    private readonly EnumParseStrategy<char, TEnum> _parseStrategy;
    private readonly EnumFormatStrategy<char, TEnum> _formatStrategy;

    /// <summary>
    /// Creates a new enum converter.
    /// </summary>
    public EnumTextConverter(CsvOptions<char> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _allowUndefinedValues = options.AllowUndefinedEnumValues;
        _ignoreCase = options.IgnoreEnumCase;
        _format = options.GetFormat(typeof(TEnum), options.EnumFormat);

        var formatStrategy = new FormatStrategy(_format);

        if (EnumMemberCache<TEnum>.IsFlagsFormat(_format))
        {
            EnumMemberCache<TEnum>.EnsureFlagsAttribute();
            _formatStrategy = new CsvEnumFlagsTextFormatStrategy<TEnum>(options, formatStrategy);
        }
        else
        {
            _formatStrategy = formatStrategy;
        }

        var parseStrategy = new ParseStrategy(_allowUndefinedValues, _ignoreCase);
        _parseStrategy = EnumMemberCache<TEnum>.HasFlagsAttribute
            ? new CsvEnumFlagsParseStrategy<char, TEnum>(options, parseStrategy)
            : parseStrategy;
    }

    /// <inheritdoc/>
    public override bool TryFormat(Span<char> destination, TEnum value, out int charsWritten)
    {
        OperationStatus status = _formatStrategy.TryFormat(destination, value, out charsWritten);

        if (status is OperationStatus.Done)
            return true;
        if (status is OperationStatus.DestinationTooSmall)
            return false;

        return Enum.TryFormat(value, destination, out charsWritten, _format);
    }

    /// <inheritdoc/>
    public override bool TryParse(ReadOnlySpan<char> source, out TEnum value)
    {
        if (_parseStrategy.TryParse(source, out value))
        {
            return true;
        }

        return Enum.TryParse(source, _ignoreCase, out value)
            && (_allowUndefinedValues || EnumCacheText<TEnum>.IsDefinedCore(value));
    }

    private sealed class ParseStrategy : EnumParseStrategy<char, TEnum>
    {
        private readonly bool _allowUndefinedValues;

        private readonly FrozenDictionary<string, TEnum>.AlternateLookup<ReadOnlySpan<char>> _values;

        public ParseStrategy(bool allowUndefinedValues, bool ignoreCase)
        {
            _allowUndefinedValues = allowUndefinedValues;
            _values = EnumCacheText<TEnum>.GetReadValues(ignoreCase);
        }

        public override bool TryParse(ReadOnlySpan<char> source, out TEnum value)
        {
            if (source.IsEmpty)
            {
                value = default;
                return false;
            }

            if (EnumExtensions.CanParseNumber<char, TEnum>(source) && EnumExtensions.TryParseNumber(source, out value))
            {
                return _allowUndefinedValues || EnumCacheText<TEnum>.IsDefinedCore(value);
            }

            if (_values.Dictionary is not null && _values.TryGetValue(source, out value))
            {
                return true;
            }

            value = default;
            return false;
        }
    }

    private sealed class FormatStrategy(string? format) : EnumFormatStrategy<char, TEnum>
    {
        private readonly FrozenDictionary<TEnum, string>? _names = EnumCacheText<TEnum>.GetWriteValues(format);

        public override OperationStatus TryFormat(Span<char> destination, TEnum value, out int charsWritten)
        {
            if (_names is not null && _names.TryGetValue(value, out string? name))
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
