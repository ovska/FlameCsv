using System.Collections.Frozen;
using System.Runtime.CompilerServices;
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

    internal EnumTextConverter(bool ignoreCase, string? format)
    {
        _allowUndefinedValues = true;
        _ignoreCase = ignoreCase;
        _format = format;
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
        if (source.IsEmpty)
        {
            value = default;
            return false;
        }

        if ((source[0] - (uint)'0') <= ('9' - '0') ||
            (
                (
                    typeof(TEnum).GetEnumUnderlyingType() == typeof(sbyte) ||
                    typeof(TEnum).GetEnumUnderlyingType() == typeof(short) ||
                    typeof(TEnum).GetEnumUnderlyingType() == typeof(int) ||
                    typeof(TEnum).GetEnumUnderlyingType() == typeof(long)
                ) &&
                source[0] == '-' // JITed away for unsigned enums
            ))
        {
            if (TryParseNumber(source, out value))
            {
                return _allowUndefinedValues || (_names?.ContainsKey(value) == true) || Enum.IsDefined(value);
            }
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

    // GetEnumUnderlyingType is intrinsic, so this method will be optimized into a single TryParse
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseNumber(ReadOnlySpan<char> source, out TEnum value)
    {
        if (typeof(TEnum).GetEnumUnderlyingType() == typeof(byte) && byte.TryParse(source, out byte b))
        {
            value = Unsafe.As<byte, TEnum>(ref b);
            return true;
        }

        if (typeof(TEnum).GetEnumUnderlyingType() == typeof(sbyte) && sbyte.TryParse(source, out sbyte sb))
        {
            value = Unsafe.As<sbyte, TEnum>(ref sb);
            return true;
        }

        if (typeof(TEnum).GetEnumUnderlyingType() == typeof(short) && short.TryParse(source, out short sh))
        {
            value = Unsafe.As<short, TEnum>(ref sh);
            return true;
        }

        if (typeof(TEnum).GetEnumUnderlyingType() == typeof(ushort) && ushort.TryParse(source, out ushort ush))
        {
            value = Unsafe.As<ushort, TEnum>(ref ush);
            return true;
        }

        if (typeof(TEnum).GetEnumUnderlyingType() == typeof(int) && int.TryParse(source, out int i))
        {
            value = Unsafe.As<int, TEnum>(ref i);
            return true;
        }

        if (typeof(TEnum).GetEnumUnderlyingType() == typeof(uint) && uint.TryParse(source, out uint ui))
        {
            value = Unsafe.As<uint, TEnum>(ref ui);
            return true;
        }

        if (typeof(TEnum).GetEnumUnderlyingType() == typeof(long) && long.TryParse(source, out long l))
        {
            value = Unsafe.As<long, TEnum>(ref l);
            return true;
        }

        if (typeof(TEnum).GetEnumUnderlyingType() == typeof(ulong) && ulong.TryParse(source, out ulong ul))
        {
            value = Unsafe.As<ulong, TEnum>(ref ul);
            return true;
        }

        Unsafe.SkipInit(out value);
        return false;
    }
}
