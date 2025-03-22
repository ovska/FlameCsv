using System.Collections.Frozen;
using FlameCsv.Utilities;

namespace FlameCsv.Converters;

/// <summary>
/// The default converter for non-flags enums.
/// </summary>
internal sealed class EnumTextConverter<TEnum> : CsvConverter<char, TEnum>
    where TEnum : struct, Enum
{
    private readonly bool _allowUndefinedValues;
    private readonly bool _ignoreCase;
    private readonly string? _format;
    private readonly FrozenDictionary<string, TEnum>.AlternateLookup<ReadOnlySpan<char>> _values;
    private readonly FrozenDictionary<TEnum, string>? _names;

    /// <summary>
    /// Creates a new enum converter.
    /// </summary>
    public EnumTextConverter(CsvOptions<char> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _allowUndefinedValues = (options.EnumOptions & CsvEnumOptions.AllowUndefinedValues) != 0;
        _ignoreCase = (options.EnumOptions & CsvEnumOptions.IgnoreCase) != 0;
        _format = options.GetFormat(typeof(TEnum), options.EnumFormat);

        bool useEnumMember = (options.EnumOptions & CsvEnumOptions.UseEnumMemberAttribute) != 0;

        if (!EnumMemberCache<TEnum>.HasFlagsAttribute)
        {
            _values = EnumCacheText<TEnum>.GetReadValues(_ignoreCase, useEnumMember);

            if (EnumMemberCache<TEnum>.IsSupported(_format))
            {
                _names = EnumCacheText<TEnum>.GetWriteValues(_format, useEnumMember);
            }
        }
    }

    /// <inheritdoc/>
    public override bool TryFormat(Span<char> destination, TEnum value, out int charsWritten)
    {
        if (_names is not null && _names.TryGetValue(value, out string? name))
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

        return Enum.TryFormat(value, destination, out charsWritten, _format);
    }

    /// <inheritdoc/>
    public override bool TryParse(ReadOnlySpan<char> source, out TEnum value)
    {
        if (EnumMemberCache<char, TEnum>.TryGetFast(source, out value))
        {
            return true;
        }

        if (_values.Dictionary is not null && _values.TryGetValue(source, out value))
        {
            // the cache never contains undefined values
            return true;
        }

        return Enum.TryParse(source, _ignoreCase, out value) &&
        (
            _allowUndefinedValues ||
            (_names?.ContainsKey(value) == true) ||
            Enum.IsDefined(value)
        );
    }
}
