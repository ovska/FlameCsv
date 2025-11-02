using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using CommunityToolkit.HighPerformance;
using FlameCsv.Reading.Internal;
using Aes = System.Runtime.Intrinsics.Arm.Aes;

namespace FlameCsv.Intrinsics;

[SkipLocalsInit]
internal static class Bithacks
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ResetLowestSetBit<T>(T mask)
        where T : unmanaged, IBinaryInteger<T>
    {
        // as of NET9, the pattern is sometimes not lowered to BLSR if inlined in a busy loop with generic math

        if (Unsafe.SizeOf<T>() is sizeof(uint) && Bmi1.IsSupported)
        {
            return Unsafe.BitCast<uint, T>(Bmi1.ResetLowestSetBit(Unsafe.BitCast<T, uint>(mask)));
        }

        if (Unsafe.SizeOf<T>() is sizeof(ulong) && Bmi1.X64.IsSupported)
        {
            return Unsafe.BitCast<ulong, T>(Bmi1.X64.ResetLowestSetBit(Unsafe.BitCast<T, ulong>(mask)));
        }

        return mask & (mask - T.One);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDisjointCR<T>(T maskLF, T shiftedCR)
        where T : unmanaged, IBinaryInteger<T>
    {
        bool nonZeroCR = shiftedCR != T.Zero;
        T xor = maskLF ^ shiftedCR;
        return nonZeroCR & xor != T.Zero;
    }

    /// <summary>
    /// Returns the mask up to the lowest set bit (<c>BLSMSK</c>).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T GetMaskUpToLowestSetBit<T>(T mask)
        where T : unmanaged, IBinaryInteger<T>
    {
        // as of NET9, the pattern is sometimes not lowered to BLSMSK if inlined in a busy loop with generic math

        if (Unsafe.SizeOf<T>() is sizeof(uint) && Bmi1.IsSupported)
        {
            return Unsafe.BitCast<uint, T>(Bmi1.GetMaskUpToLowestSetBit(Unsafe.BitCast<T, uint>(mask)));
        }

        if (Unsafe.SizeOf<T>() is sizeof(ulong) && Bmi1.X64.IsSupported)
        {
            return Unsafe.BitCast<ulong, T>(Bmi1.X64.GetMaskUpToLowestSetBit(Unsafe.BitCast<T, ulong>(mask)));
        }

        return mask ^ (mask - T.One); // lowered to blsmsk on x86
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetSubractionFlag<TNewline>(bool noCR)
        where TNewline : struct, INewline
    {
        uint flag = Field.IsEOL;

        if (TNewline.IsCRLF)
        {
            int mask = noCR.ToByte() - 1;
            flag ^= (uint)(mask & 0xC0000001u);
        }

        return flag;
    }

    /// <summary>
    /// Returns the subraction flag for the given newline mask at bit <paramref name="tz"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ProcessFlag<T>(T maskNewline, uint tz, uint flag)
        where T : unmanaged, IBinaryInteger<T>
    {
        uint newlineBit = uint.CreateTruncating(maskNewline >> (int)tz) & 1;
        return (uint)(-(int)newlineBit & flag);
    }

    /// <summary>
    /// Checks if all bits in the mask are before the first bit set in the other value.
    /// </summary>
    /// <param name="mask">A non-zero bitmask</param>
    /// <param name="other">A non-zero bitmask</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AllBitsBefore(nuint mask, nuint other)
    {
        Debug.Assert(mask > 0 && other > 0, $"Both mask and other must be non-zero, were: {mask:B} and {other:B}");

        // this is a slight optimization over using BitOperations.Log2 directly, as it must ensure that mask is not zero
        if (nuint.Size == 8)
        {
            return (63 ^ BitOperations.LeadingZeroCount(mask)) < BitOperations.TrailingZeroCount(other);
        }

        return (31 ^ BitOperations.LeadingZeroCount(mask)) < BitOperations.TrailingZeroCount(other);
    }

    /// <summary>
    /// Finds the quote mask for the current iteration, where bits between quotes are all 1's.
    /// </summary>
    /// <param name="quoteBits">Bitmask of the quote positions</param>
    /// <param name="quoteCount">
    /// How many quotes the current field has (if any).
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong FindQuoteMask(ulong quoteBits, uint quoteCount)
    {
        ulong quoteMask = ComputeQuoteMask(quoteBits);
        ulong mask = 0 - (quoteCount & 1);
        return quoteMask ^ mask;
    }

    /// <inheritdoc cref="FindQuoteMask(ulong, uint)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint FindQuoteMask(uint quoteBits, uint quoteCount)
    {
        uint quoteMask = ComputeQuoteMask(quoteBits);
        uint mask = 0 - (quoteCount & 1);
        return quoteMask ^ mask;
    }

    /// <summary>
    /// Flips all bits in <paramref name="value"/> is condition is odd.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ConditionalFlipQuotes<T>(ref T value, T condition)
        where T : unmanaged, IBinaryInteger<T>
    {
        T mask = T.Zero - (condition & T.One); // Extract LSB and create all-1s or all-0s mask
        value ^= mask;
    }

    /// <summary>
    /// Returns <c>true</c> if the value has a popcount of 0 or 1.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ZeroOrOneBitsSet<T>(T value)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (Unsafe.SizeOf<T>() is sizeof(uint) && Bmi1.IsSupported)
        {
            return Bmi1.ResetLowestSetBit(Unsafe.BitCast<T, uint>(value)) == 0;
        }

        if (Unsafe.SizeOf<T>() is sizeof(ulong) && Bmi1.X64.IsSupported)
        {
            return Bmi1.X64.ResetLowestSetBit(Unsafe.BitCast<T, ulong>(value)) == 0;
        }

        return (value & (value - T.One)) == T.Zero;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong ComputeQuoteMask(ulong quoteBits)
    {
        if (Pclmulqdq.IsSupported && Sse2.IsSupported)
        {
            var vec = Vector128.CreateScalar(quoteBits);
            var result = Pclmulqdq.CarrylessMultiply(vec, Vector128<ulong>.AllBitsSet, 0);
            return result.GetElement(0);
        }

        if (Aes.IsSupported)
        {
            Vector64<ulong> quoteBitsVec = Vector64.Create(quoteBits);
            Vector128<ulong> prod = Aes.PolynomialMultiplyWideningLower(quoteBitsVec, Vector64<ulong>.AllBitsSet);
            return prod.GetElement(0);
        }

        return ComputeQuoteMaskSoftwareFallback(quoteBits);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint ComputeQuoteMask(uint quoteBits)
    {
        if (Pclmulqdq.IsSupported && Sse2.IsSupported)
        {
            var vec = Vector128.CreateScalar((ulong)quoteBits);
            var result = Pclmulqdq.CarrylessMultiply(vec, Vector128<ulong>.AllBitsSet, 0);
            return (uint)result.GetElement(0);
        }

        if (Aes.IsSupported)
        {
            Vector64<ulong> quoteBitsVec = Vector64.Create((ulong)quoteBits);
            Vector128<ulong> prod = Aes.PolynomialMultiplyWideningLower(quoteBitsVec, Vector64<ulong>.AllBitsSet);
            return (uint)prod.GetElement(0);
        }

        return ComputeQuoteMaskSoftwareFallback(quoteBits);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint ComputeQuoteMaskSoftwareFallback(uint quoteBits)
    {
        uint mask = quoteBits ^ (quoteBits << 1);
        mask ^= (mask << 2);
        mask ^= (mask << 4);
        mask ^= (mask << 8);
        mask ^= (mask << 16);
        return mask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong ComputeQuoteMaskSoftwareFallback(ulong quoteBits)
    {
        ulong mask = quoteBits ^ (quoteBits << 1);
        mask ^= (mask << 2);
        mask ^= (mask << 4);
        mask ^= (mask << 8);
        mask ^= (mask << 16);
        mask ^= (mask << 32);
        return mask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCRLF<T>(ref T value)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (Unsafe.SizeOf<T>() is sizeof(byte))
        {
            ushort crlf = MemoryMarshal.Read<ushort>("\r\n"u8);
            return Unsafe.As<T, ushort>(ref value) == crlf;
        }

        if (Unsafe.SizeOf<T>() is sizeof(char))
        {
            uint crlf = MemoryMarshal.Read<uint>(MemoryMarshal.Cast<char, byte>("\r\n"));
            return Unsafe.As<T, uint>(ref value) == crlf;
        }

        throw Token<T>.NotSupported;
    }
}
