using System.Diagnostics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;
using FlameCsv.Reflection;

namespace FlameCsv.Converters.Enums;

/// <summary>
/// Provides a wrapper for parsing flags-enums using an inner strategy.<br/>
/// Fields are split with the <see cref="CsvOptions{T}.EnumFlagsSeparator"/> character and parsed using the inner strategy.
/// </summary>
public sealed class CsvEnumFlagsParseStrategy<T, TEnum> : EnumParseStrategy<T, TEnum>
    where T : unmanaged, IBinaryInteger<T>
    where TEnum : struct, Enum
{
    private readonly T _separator;
    private readonly EnumParseStrategy<T, TEnum> _inner;
    private readonly CsvFieldTrimming _trimming;
    private readonly bool _allowUndefinedValues;

    /// <summary>
    /// Initializes a new flags-parse strategy.
    /// </summary>
    public CsvEnumFlagsParseStrategy(CsvOptions<T> options, EnumParseStrategy<T, TEnum> inner)
    {
        Debug.Assert(EnumMemberCache<TEnum>.HasFlagsAttribute, $"Enum {typeof(TEnum)} is not a flags-enum");

        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(inner);

        EnumMemberCache<TEnum>.EnsureValidFlagsSeparator(options.EnumFlagsSeparator);

        options.MakeReadOnly();
        _separator = T.CreateChecked(options.EnumFlagsSeparator);
        _inner = inner;
        _allowUndefinedValues = options.AllowUndefinedEnumValues;
        _trimming = options.Trimming;
    }

    /// <inheritdoc/>
    public override bool TryParse(ReadOnlySpan<T> source, out TEnum value)
    {
        if (source.IsEmpty)
        {
            value = default;
            return false;
        }

        if (EnumExtensions.CanParseNumber<T, TEnum>(source))
        {
            bool retVal;

            if (typeof(T) == typeof(byte))
            {
                retVal = EnumExtensions.TryParseNumber(source.Cast<T, byte>(), out value);
            }
            else if (typeof(T) == typeof(char))
            {
                retVal = EnumExtensions.TryParseNumber(source.Cast<T, char>(), out value);
            }
            else
            {
                value = default;
                return false;
            }

            return retVal && (_allowUndefinedValues || EnumCacheText<TEnum>.IsDefinedCore(value));
        }

        bool parsedAny = false;
        value = default;

        foreach (var part in source.Split(_separator))
        {
            ReadOnlySpan<T> slice = source[part];

            if ((_trimming & CsvFieldTrimming.Both) != CsvFieldTrimming.None)
            {
                slice = slice.Trim(_trimming);
            }

            if (slice.IsEmpty)
            {
                // should this return false?
                continue;
            }

            if (_inner.TryParse(slice, out var flag))
            {
                value.AddFlag(flag);
                parsedAny = true;
            }
            else
            {
                return false;
            }
        }

        return parsedAny;
    }
}
