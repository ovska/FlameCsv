using System.Runtime.CompilerServices;
using System.Text;
using FlameCsv.Reading.Internal;
using FlameCsv.Reading.Unescaping;
using FlameCsv.Reflection;

namespace FlameCsv.Tests;

public class Bithacks
{
    public static TheoryData<uint, uint, uint, uint> OddSequenceData
        => new()
        {
            { 0u, 0u, 0u, 0u },
            { 0b0011, 0u, 0u, 0u },
            { 0b0001, 1u, 0u, 0u },
            { 0b01110, 0u, 0b10000, 0u },
            { 0b00001, 0u, 0b0010u, 0u },
            { 0b00011, 0u, 0u, 0u },
            { 0b00111, 0u, 0b01000u, 0u },
            { 0b00001, 1u, 0u, 0u },
            { 0b00011, 1u, 0b0100u, 0u },
            { 0b00111, 1u, 0u, 0u },

            // pathological inputs
            { 0xffffffffu, 0u, 0u, 0u },
            { 0xffffffffu, 1u, 0u, 1u },
            { 0xffffffffu << 1, 1u, 1u, 1u },
            { 0xffffffffu >> 1, 0u, 1u << 31, 0u },
            { 0xAAAAAAAAu, 0u, 0x55555555u - 1, 1u }, // 101010...
            { 0x55555555u, 0u, 0xAAAAAAAAu, 0u }, // 010101...
        };

    [Theory]
    [MemberData(nameof(OddSequenceData))]
    public static void Test(uint input, uint carry, uint expected, uint expectedCarry)
    {
        uint carryArg = carry;
        uint oddEndsMask = find_odd_backslash_sequences(input, ref carryArg);

        if (expected != oddEndsMask || expectedCarry != carryArg)
        {
            Assert.Fail(
                "Odd sequnce did not match:" +
                Environment.NewLine +
                $"Input:    {input:b32} (carry {carry})" +
                Environment.NewLine +
                $"Actual:   {oddEndsMask:b32} (carry {carryArg})" +
                Environment.NewLine +
                $"Expected: {expected:b32} (carry {expectedCarry})");
        }
    }

    internal static uint find_odd_backslash_sequences(uint bs_bits, ref uint prev_iter_ends_odd_backslash)
    {
        const uint even_bits = 0x55555555u;
        const uint odd_bits = ~even_bits;
        uint start_edges = bs_bits & ~(bs_bits << 1);
        // flip lowest if we have an odd-length run at the end of the prior
        // iteration
        uint even_start_mask = even_bits ^ prev_iter_ends_odd_backslash;
        uint even_starts = start_edges & even_start_mask;
        uint odd_starts = start_edges & ~even_start_mask;
        uint even_carries = bs_bits + even_starts;

        // must record the carry-out of our odd-carries out of bit 63; this
        // indicates whether the sense of any edge going to the next iteration
        // should be flipped
        bool iter_ends_odd_backslash = addcarry(bs_bits, odd_starts, out uint odd_carries);

        odd_carries |= prev_iter_ends_odd_backslash; // push in bit zero as a potential end
        // if we had an odd-numbered run at the
        // end of the previous iteration
        prev_iter_ends_odd_backslash = iter_ends_odd_backslash ? 1u : 0u;
        uint even_carry_ends = even_carries & ~bs_bits;
        uint odd_carry_ends = odd_carries & ~bs_bits;
        uint even_start_odd_end = even_carry_ends & odd_bits;
        uint odd_start_even_end = odd_carry_ends & even_bits;
        uint odd_ends = even_start_odd_end | odd_start_even_end;
        return odd_ends;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool addcarry(uint value1, uint value2, out uint result)
    {
        result = value1 + value2;
        return result < value1;
    }
}
