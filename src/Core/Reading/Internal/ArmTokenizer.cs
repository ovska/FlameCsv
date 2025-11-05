using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using FlameCsv.Intrinsics;

namespace FlameCsv.Reading.Internal;

internal static class ArmTokenizer
{
    public static bool IsSupported => AdvSimd.Arm64.IsSupported && ArmBase.Arm64.IsSupported;
}

[SkipLocalsInit]
internal sealed class ArmTokenizer<T, TNewline> : CsvPartialTokenizer<T>
    where T : unmanaged, IBinaryInteger<T>
    where TNewline : struct, INewline
{
    private static nuint EndOffset
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (nuint)MaxFieldsPerIteration * 2;
    }

    private static int MaxFieldsPerIteration
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector128<byte>.Count * 4;
    }

    public override int PreferredLength => MaxFieldsPerIteration * 4;

    public override int MinimumFieldBufferSize => MaxFieldsPerIteration;

    private readonly T _quote;
    private readonly T _delimiter;

    public ArmTokenizer(CsvOptions<T> options)
    {
        _quote = T.CreateTruncating(options.Quote);
        _delimiter = T.CreateTruncating(options.Delimiter);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override int Tokenize(FieldBuffer buffer, int startIndex, ReadOnlySpan<T> data)
    {
        throw new NotImplementedException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ParseDelimitersAndNewlines(
        uint count,
        ulong mask,
        ulong maskLF,
        ulong shiftedCR,
        uint index,
        ref uint dst
    )
    {
        // on 128bit vectors 3 is optimal; revisit if we change width
        const uint unrollCount = 5;

        uint lfPos = (uint)BitOperations.PopCount(mask & (maskLF - 1));

        Unsafe.Add(ref dst, 0u) = index + (uint)BitOperations.TrailingZeroCount(mask);
        Unsafe.Add(ref dst, 1u) = index + (uint)BitOperations.TrailingZeroCount(mask &= mask - 1);
        Unsafe.Add(ref dst, 2u) = index + (uint)BitOperations.TrailingZeroCount(mask &= mask - 1);
        Unsafe.Add(ref dst, 3u) = index + (uint)BitOperations.TrailingZeroCount(mask &= mask - 1);
        Unsafe.Add(ref dst, 4u) = index + (uint)BitOperations.TrailingZeroCount(mask &= mask - 1);

        if (count > unrollCount)
        {
            // for some reason this is faster than incrementing a pointer
            ref uint dst2 = ref Unsafe.Add(ref dst, unrollCount);

            do
            {
                uint offset = (uint)BitOperations.TrailingZeroCount(mask &= mask - 1);
                dst2 = index + offset;
                dst2 = ref Unsafe.Add(ref dst2, 1u);
            } while (mask != 0);
        }

        uint lfTz = (uint)BitOperations.TrailingZeroCount(maskLF);
        Unsafe.Add(ref dst, lfPos) = index + lfTz - Bithacks.GetSubractionFlag<TNewline>(shiftedCR == 0);
    }
}
