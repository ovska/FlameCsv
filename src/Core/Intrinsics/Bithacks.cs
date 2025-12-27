using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using FlameCsv.Reading.Internal;
using ArmAes = System.Runtime.Intrinsics.Arm.Aes;

namespace FlameCsv.Intrinsics;

[SkipLocalsInit]
internal static class Bithacks
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetQuoteFlags(uint quoteCount)
    {
        Check.True(quoteCount % 2 == 0);

        byte any = Unsafe.BitCast<bool, byte>(quoteCount != 0);
        byte needsUnescaping = Unsafe.BitCast<bool, byte>(quoteCount > 2);
        return ((uint)any << 29) | ((uint)needsUnescaping << 28);
    }

    /// <summary>
    /// Resets the lowest set bit in <paramref name="mask"/> (<c>BLSR</c> or emulation).
    /// </summary>
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

    /// <summary>
    /// Returns whether there are CR bits set, the CR and LF bits do not form well-formed CRLF sequences.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDisjointCR<T>(T maskLF, T shiftedCR)
        where T : unmanaged, IBinaryInteger<T>
    {
        bool nonZeroCR = shiftedCR != T.Zero;
        T xor = maskLF ^ shiftedCR;
        return nonZeroCR & xor != T.Zero;
    }

    /// <summary>
    /// Returns the mask up to the lowest set bit (<c>BLSMSK</c> or emulation).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T GetMaskUpToLowestSetBit<T>(T mask)
        where T : unmanaged, IBinaryInteger<T>
    {
        Check.NotEqual(mask, T.Zero);

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

    /// <summary>
    /// Returns the subraction flag for the given newline setting. If <paramref name="noCR"/> is <c>true</c>,
    /// subracting the EOL flag will set the LF bit only; otherwise 1 is subtracted from the value, along with
    /// both CR and LF bits being set.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetSubractionFlag(bool noCR)
    {
        int mask = Unsafe.BitCast<bool, byte>(noCR) - 1;
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
            int mask = -(int)(((ulong)Unsafe.BitCast<T, uint>(maskNewline) >> (int)pos) & 1UL);
            return flag & (uint)mask;
        }

        if (Unsafe.SizeOf<T>() is sizeof(ulong))
        {
            // handle pos=64 explicitly (tzcnt 0 returns)
            int mask = -(int)((Unsafe.BitCast<T, ulong>(maskNewline) >> (int)pos) & 1UL);
            int validMask = ((int)pos - 64) >> 31;
            return flag & (uint)(mask & validMask);
        }

        throw new NotSupportedException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint FindInverseQuoteMaskSingle(uint maskQuote, uint quotesConsumed)
    {
        Check.Equal(BitOperations.PopCount(maskQuote), 1);
        uint before = maskQuote - 1u;
        uint mask = 0u - (quotesConsumed & 1u); // 0 or 0xFFFFFFFF
        return before ^ mask;
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
    /// Returns <c>true</c> if the value has a population count of 0 or 1.
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

    /// <summary>
    /// Returns <c>true</c> if the value has a population count of 2 or more.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TwoOrMoreBitsSet<T>(T value)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (Unsafe.SizeOf<T>() is sizeof(uint) && Bmi1.IsSupported)
        {
            uint v = Unsafe.BitCast<T, uint>(value);
            return v != 0 & Bmi1.ResetLowestSetBit(v) != 0;
        }

        if (Unsafe.SizeOf<T>() is sizeof(ulong) && Bmi1.X64.IsSupported)
        {
            ulong v = Unsafe.BitCast<T, ulong>(value);
            return v != 0 & Bmi1.X64.ResetLowestSetBit(v) != 0;
        }

        return value != T.Zero & (value & (value - T.One)) != T.Zero;
    }

    /// <summary>
    /// Does a prefix XOR-based computation of the quote mask.
    /// </summary>
    /// <param name="quoteBits">Movemask result containing bits at quote positions</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static T ComputeQuoteMask<T>(T quoteBits)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (Pclmulqdq.IsSupported)
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

    /// <summary>
    /// Returns whether the given value is the start of a CRLF sequence (reads one value past the reference).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCRLF<T>(ref T value)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (Unsafe.SizeOf<T>() is sizeof(byte))
        {
            ushort crlf = MemoryMarshal.Read<ushort>("\r\n"u8); // constant folded
            return Unsafe.As<T, ushort>(ref value) == crlf;
        }

        if (Unsafe.SizeOf<T>() is sizeof(char))
        {
            uint crlf = MemoryMarshal.Read<uint>(MemoryMarshal.Cast<char, byte>("\r\n")); // constant folded
            return Unsafe.As<T, uint>(ref value) == crlf;
        }

        throw Token<T>.NotSupported;
    }

    /// <summary>
    /// Reverses the bits in the given value.
    /// </summary>
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

    /// <summary>
    /// Isolates the lowest <paramref name="count"/> bits in an <see cref="UInt32"/>.
    /// </summary>
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
}
