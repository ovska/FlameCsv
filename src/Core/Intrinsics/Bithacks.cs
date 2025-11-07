using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using CommunityToolkit.HighPerformance;
using FlameCsv.Reading.Internal;
using ArmAes = System.Runtime.Intrinsics.Arm.Aes;

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
    public static uint GetSubractionFlag(bool noCR)
    {
        int mask = noCR.ToByte() - 1;
        uint flag = Field.IsEOL;
        flag ^= (uint)(mask & 0xC0000001u);
        return flag;
    }

    /// <summary>
    /// Returns the subraction flag for the given newline mask at bit <paramref name="pos"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ProcessFlag<T>(T maskNewline, uint pos, uint flag)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (Unsafe.SizeOf<T>() is sizeof(uint))
        {
            // handle pos=32 with zero extension to uint64 (tzcnt 0 returns)
            bool set = (((ulong)Unsafe.BitCast<T, uint>(maskNewline) >> (int)pos) & 1UL) != 0;
            return set ? flag : 0u;
        }

        if (Unsafe.SizeOf<T>() is sizeof(ulong))
        {
            // handle pos=64 explicitly (tzcnt 0 returns)
            ulong mask = Unsafe.BitCast<T, ulong>(maskNewline);
            int validMask = ((int)pos - 64) >> 31;
            bool set = ((mask >> (int)pos) & 1UL & (uint)validMask) != 0;
            return set ? flag : 0u;
        }

        return default;
    }

    /// <summary>
    /// Finds the quote mask for the current iteration, where bits between quotes are all 1's.
    /// </summary>
    /// <param name="quoteBits">Bitmask of the quote positions</param>
    /// <param name="quoteCount">
    /// How many quotes the current field has (if any).
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static T FindQuoteMask<T>(T quoteBits, uint quoteCount)
        where T : unmanaged, IBinaryInteger<T>
    {
        T quoteMask = ComputeQuoteMask(quoteBits);
        T mask = T.Zero - (T.CreateTruncating(quoteCount) & T.One);
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
    internal static T ComputeQuoteMask<T>(T quoteBits)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (Pclmulqdq.IsSupported && Sse2.IsSupported)
        {
            var vec = Vector128.CreateScalar(ulong.CreateTruncating(quoteBits));
            var result = Pclmulqdq.CarrylessMultiply(vec, Vector128<ulong>.AllBitsSet, 0);
            return T.CreateTruncating(result.GetElement(0));
        }

        if (ArmAes.IsSupported)
        {
            var r = ArmAes.PolynomialMultiplyWideningLower(
                Vector64.Create(ulong.CreateTruncating(quoteBits)),
                Vector64<ulong>.AllBitsSet
            );

            return T.CreateTruncating(r.GetElement(0));
        }

        return ComputeQuoteMaskSoftwareFallback(quoteBits);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static T ComputeQuoteMaskSoftwareFallback<T>(T quoteBits)
        where T : unmanaged, IBinaryInteger<T>
    {
        T mask = quoteBits ^ (quoteBits << 1);
        mask ^= (mask << 2);
        mask ^= (mask << 4);
        if (Unsafe.SizeOf<T>() >= sizeof(ushort))
            mask ^= (mask << 8);
        if (Unsafe.SizeOf<T>() >= sizeof(uint))
            mask ^= (mask << 16);
        if (Unsafe.SizeOf<T>() >= sizeof(ulong))
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public static T ReverseBits<T>(T v)
        where T : unmanaged, IBinaryInteger<T>
    {
        return Unsafe.SizeOf<T>() switch
        {
            sizeof(uint) => Unsafe.BitCast<uint, T>(ArmBase.ReverseElementBits(Unsafe.BitCast<T, uint>(v))),
            sizeof(ulong) => Unsafe.BitCast<ulong, T>(ArmBase.Arm64.ReverseElementBits(Unsafe.BitCast<T, ulong>(v))),
            _ => throw new NotSupportedException(typeof(T).FullName),
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [DebuggerStepThrough]
    public static T IsolateLowestBits<T>(T value, uint count)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (Unsafe.SizeOf<T>() is sizeof(uint))
        {
            ulong result = (ulong)Unsafe.BitCast<T, uint>(value) << (int)(32u - count);
            return Unsafe.BitCast<uint, T>((uint)result);
        }

        throw new NotSupportedException(typeof(T).FullName);
    }

    public static ReadOnlySpan<uint> LowestBits =>
        [
            0b0000_0000_0000_0000_0000_0000_0000_0000,
            0b0000_0000_0000_0000_0000_0000_0000_0001,
            0b0000_0000_0000_0000_0000_0000_0000_0011,
            0b0000_0000_0000_0000_0000_0000_0000_0111,
            0b0000_0000_0000_0000_0000_0000_0000_1111,
            0b0000_0000_0000_0000_0000_0000_0001_1111,
            0b0000_0000_0000_0000_0000_0000_0011_1111,
            0b0000_0000_0000_0000_0000_0000_0111_1111,
            0b0000_0000_0000_0000_0000_0000_1111_1111,
            0b0000_0000_0000_0000_0000_0001_1111_1111,
            0b0000_0000_0000_0000_0000_0011_1111_1111,
            0b0000_0000_0000_0000_0000_0111_1111_1111,
            0b0000_0000_0000_0000_0000_1111_1111_1111,
            0b0000_0000_0000_0000_0001_1111_1111_1111,
            0b0000_0000_0000_0000_0011_1111_1111_1111,
            0b0000_0000_0000_0000_0111_1111_1111_1111,
            0b0000_0000_0000_0000_1111_1111_1111_1111,
            0b0000_0000_0000_0001_1111_1111_1111_1111,
            0b0000_0000_0000_0011_1111_1111_1111_1111,
            0b0000_0000_0000_0111_1111_1111_1111_1111,
            0b0000_0000_0000_1111_1111_1111_1111_1111,
            0b0000_0000_0001_1111_1111_1111_1111_1111,
            0b0000_0000_0011_1111_1111_1111_1111_1111,
            0b0000_0000_0111_1111_1111_1111_1111_1111,
            0b0000_0000_1111_1111_1111_1111_1111_1111,
            0b0000_0001_1111_1111_1111_1111_1111_1111,
            0b0000_0011_1111_1111_1111_1111_1111_1111,
            0b0000_0111_1111_1111_1111_1111_1111_1111,
            0b0000_1111_1111_1111_1111_1111_1111_1111,
            0b0001_1111_1111_1111_1111_1111_1111_1111,
            0b0011_1111_1111_1111_1111_1111_1111_1111,
            0b0111_1111_1111_1111_1111_1111_1111_1111,
            0b1111_1111_1111_1111_1111_1111_1111_1111,
            0b1111_1111_1111_1111_1111_1111_1111_1111,
        ];
}
