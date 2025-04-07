using System.Buffers;
using System.Diagnostics;
using FlameCsv.Extensions;
using FlameCsv.Utilities;

namespace FlameCsv.Converters;

/// <summary>
/// Provides a wrapper for formatting flags-enums using an inner strategy.<br/>
/// Fields are formatted with the <see cref="CsvOptions{T}.EnumFlagsSeparator"/> character and parsed using the inner strategy.
/// </summary>
/// <remarks>
/// Falls back to writing a numeric value if the value contains a flag not defined in the enum type.
/// </remarks>
public abstract class CsvEnumFlagsFormatStrategy<T, TEnum> : EnumFormatStrategy<T, TEnum>
    where T : unmanaged, IBinaryInteger<T>
    where TEnum : struct, Enum
{
    private static readonly TEnum[] _valuesByBits;

    static CsvEnumFlagsFormatStrategy()
    {
        _valuesByBits = Enum.GetValues<TEnum>().Distinct().ToArray();
        _valuesByBits
            .AsSpan()
            .Sort(
                static (a, b) =>
                {
                    int cmp = b.PopCount().CompareTo(a.PopCount());
                    if (cmp == 0) cmp = b.ToBitmask().CompareTo(a.ToBitmask());
                    return cmp;
                });
    }

    private readonly T _separator;
    private readonly ulong _allFlags;
    private readonly EnumFormatStrategy<T, TEnum> _inner;

    /// <summary>
    /// Initializes a new flags-format strategy.
    /// </summary>
    protected CsvEnumFlagsFormatStrategy(CsvOptions<T> options, EnumFormatStrategy<T, TEnum> inner)
    {
        Debug.Assert(EnumMemberCache<TEnum>.HasFlagsAttribute, $"Enum {typeof(TEnum)} is not a flags-enum");

        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(inner);

        EnumMemberCache<TEnum>.EnsureValidFlagsSeparator(options.EnumFlagsSeparator);

        _separator = T.CreateChecked(options.EnumFlagsSeparator);
        _inner = inner;
        _allFlags = EnumMemberCache<TEnum>.AllFlags.ToBitmask();
    }

    /// <summary>
    /// Returns the name of the zero-value with no flags.
    /// </summary>
    protected abstract ReadOnlySpan<T> Zero { get; }

    /// <summary>
    /// Returns whether all the flags in the value are defined in the enum type.
    /// </summary>
    protected bool AllFlagsDefined(TEnum value)
    {
        return (value.ToBitmask() & ~_allFlags) == 0;
    }

    /// <inheritdoc/>
    public override OperationStatus TryFormat(Span<T> destination, TEnum value, out int charsWritten)
    {
        // fast path for zero
        if (EqualityComparer<TEnum>.Default.Equals(value, default))
        {
            return Zero.TryCopyTo(destination, out charsWritten)
                ? OperationStatus.Done
                : OperationStatus.DestinationTooSmall;
        }

        charsWritten = 0;

        // refer to number formatting if the value contains any bit not defined in the enum type
        if (!AllFlagsDefined(value))
        {
            return OperationStatus.InvalidData;
        }

        Span<int> foundValues = stackalloc int[BitOperations.PopCount(value.ToBitmask())];
        int count = 0;

        while (!EqualityComparer<TEnum>.Default.Equals(value, default))
        {
            int current = -1;

            for (int i = 0; i < _valuesByBits.Length; i++)
            {
                var member = _valuesByBits[i];

                if (value.HasFlag(member))
                {
                    value.ClearFlag(member);
                    current = i;
                    break;
                }
            }

            if (current == -1) return OperationStatus.InvalidData;
            foundValues[count++] = current;
        }

        foundValues = foundValues.Slice(0, count);
        foundValues.Reverse();

        bool first = true;

        foreach (int index in foundValues)
        {
            TEnum flag = _valuesByBits[index];

            if (first)
            {
                first = false;
            }
            else
            {
                if (destination.IsEmpty) return OperationStatus.DestinationTooSmall;
                destination[0] = _separator;
                destination = destination.Slice(1);
                charsWritten++;
            }

            OperationStatus status = _inner.TryFormat(destination, flag, out int written);

            if (status != OperationStatus.Done)
            {
                return status;
            }

            charsWritten += written;
            destination = destination.Slice(written);
        }

        return OperationStatus.Done;
    }
}
