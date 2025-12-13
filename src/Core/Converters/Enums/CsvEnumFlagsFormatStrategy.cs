using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using FlameCsv.Reflection;

namespace FlameCsv.Converters.Enums;

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
        _valuesByBits = [.. Enum.GetValues<TEnum>().Distinct()];
        _valuesByBits
            .AsSpan()
            .Sort(
                static (a, b) =>
                {
                    ulong aMask = a.ToBitmask();
                    ulong bMask = b.ToBitmask();
                    int aBits = BitOperations.PopCount(aMask);
                    int bBits = BitOperations.PopCount(bMask);

                    if (aBits != bBits)
                    {
                        return bBits.CompareTo(aBits);
                    }

                    return bMask.CompareTo(aMask);
                }
            );
    }

    private readonly T _separator;
    private readonly ulong _undefinedFlags;
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
        _undefinedFlags = ~EnumMemberCache<TEnum>.AllFlags.ToBitmask();
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
        return (value.ToBitmask() & _undefinedFlags) == 0;
    }

    /// <inheritdoc/>
    public override OperationStatus TryFormat(Span<T> destination, TEnum value, out int charsWritten)
    {
        charsWritten = 0;

        // fast path for zero
        if (EqualityComparer<TEnum>.Default.Equals(value, default))
        {
            ReadOnlySpan<T> zero = Zero;

            if (zero.TryCopyTo(destination))
            {
                charsWritten = zero.Length;
                return OperationStatus.Done;
            }

            return OperationStatus.DestinationTooSmall;
        }

        // defer to number formatting if the value contains any bit not defined in the enum type
        if (!AllFlagsDefined(value))
        {
            return OperationStatus.InvalidData;
        }

        // allocate a buffer for each bit
        // constant sized stackallocs are treated nicer (JIT constant folds sizeof)
        Span<int> foundValues = stackalloc int[Unsafe.SizeOf<TEnum>() * 8];
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

            if (current == -1)
                return OperationStatus.InvalidData;
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
                if (destination.IsEmpty)
                    return OperationStatus.DestinationTooSmall;
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
