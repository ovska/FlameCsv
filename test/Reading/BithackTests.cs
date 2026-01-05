using System.Diagnostics;
using System.Runtime.CompilerServices;
using FlameCsv.Intrinsics;

namespace FlameCsv.Tests.Reading;

public class BithackTests
{
    [Fact]
    public static void Should_Get_Flag()
    {
        const uint flag = 123u;
        Assert.Equal(flag, Bithacks.ProcessFlag(1, pos: 0, flag));
        Assert.Equal(0u, Bithacks.ProcessFlag(1, pos: 1, flag));
        Assert.Equal(flag, Bithacks.ProcessFlag(1 << 31, uint.TrailingZeroCount(1u << 31), flag));
        Assert.Equal(flag, Bithacks.ProcessFlag(1 << 5, uint.TrailingZeroCount(1u << 5), flag));
        Assert.Equal(0u, Bithacks.ProcessFlag(~(1 << 5), uint.TrailingZeroCount(1u << 5), flag));
        Assert.Equal(0u, Bithacks.ProcessFlag(1 << 31, pos: uint.TrailingZeroCount(0), flag));
        Assert.Equal(flag, Bithacks.ProcessFlag(1, pos: uint.TrailingZeroCount(0), flag));
        Assert.Equal(0u, Bithacks.ProcessFlag(0b10, pos: uint.TrailingZeroCount(0), flag));

        Assert.Equal(flag, Bithacks.ProcessFlag(1ul, pos: 0, flag));
        Assert.Equal(0u, Bithacks.ProcessFlag(1ul, pos: 1, flag));
        Assert.Equal(flag, Bithacks.ProcessFlag(1ul << 63, (uint)ulong.TrailingZeroCount(1ul << 63), flag));
        Assert.Equal(flag, Bithacks.ProcessFlag(1ul << 5, (uint)ulong.TrailingZeroCount(1ul << 5), flag));
        Assert.Equal(0u, Bithacks.ProcessFlag(~(1ul << 5), (uint)ulong.TrailingZeroCount(1ul << 5), flag));
        Assert.Equal(0u, Bithacks.ProcessFlag(1ul << 63, pos: (uint)ulong.TrailingZeroCount(0), flag));
        Assert.Equal(flag, Bithacks.ProcessFlag(1ul, pos: (uint)ulong.TrailingZeroCount(0), flag));
        Assert.Equal(0u, Bithacks.ProcessFlag(0b10ul, pos: uint.TrailingZeroCount(0), flag));

        Assert.Throws<NotSupportedException>(() => Bithacks.ProcessFlag((ushort)1, 0, flag));
    }

    [Theory]
    [InlineData("0", "0", false)]
    [InlineData("0", "1", false)]
    [InlineData("1", "1", false)]
    [InlineData("1", "0", true)]
    [InlineData("000100", "000100", false)]
    [InlineData("000000", "000100", false)]
    [InlineData("000100", "010100", true)]
    [InlineData("010000", "010100", true)]
    [InlineData("010100", "010000", true)]
    [InlineData("010100", "000100", true)]
    [InlineData("000100", "000000", true)]
    public static void Should_Check_If_Disjoint_CR(string cr, string lf, bool expected)
    {
        Assert.Equal(expected, Bithacks.IsDisjointCR(Convert.ToUInt32(lf, 2), Convert.ToUInt32(cr, 2)));
    }

    [Fact]
    public static void Should_Get_Mask_Up_To_Lowest_Set_Bit()
    {
        Assert.Equal(0b0001u, Bithacks.GetMaskUpToLowestSetBit(0b0001u));
        Assert.Equal(0b0011u, Bithacks.GetMaskUpToLowestSetBit(0b0010u));
        Assert.Equal(0b0001u, Bithacks.GetMaskUpToLowestSetBit(0b0011u));
        Assert.Equal(0b0111u, Bithacks.GetMaskUpToLowestSetBit(0b0100u));
        Assert.Equal(0b0011u, Bithacks.GetMaskUpToLowestSetBit(0b0110u));
        Assert.Equal(0b0001u, Bithacks.GetMaskUpToLowestSetBit(0b0111u));
        Assert.Equal(0b1111u, Bithacks.GetMaskUpToLowestSetBit(0b1000u));
        Assert.Equal(0b0011u, Bithacks.GetMaskUpToLowestSetBit(0b1010u));
        Assert.Equal(0b0001u, Bithacks.GetMaskUpToLowestSetBit(0b1011u));
        Assert.Equal(0b0001u, Bithacks.GetMaskUpToLowestSetBit(0b1111u));
        Assert.Equal(0b1111u, Bithacks.GetMaskUpToLowestSetBit(0b1000u));
        Assert.Equal(0b0001ul, Bithacks.GetMaskUpToLowestSetBit(0b0001ul));
        Assert.Equal(0b0011ul, Bithacks.GetMaskUpToLowestSetBit(0b0010ul));
        Assert.Equal(0b0001ul, Bithacks.GetMaskUpToLowestSetBit(0b0011ul));
        Assert.Equal(0b0111ul, Bithacks.GetMaskUpToLowestSetBit(0b0100ul));
        Assert.Equal(0b0011ul, Bithacks.GetMaskUpToLowestSetBit(0b0110ul));
        Assert.Equal(0b0001ul, Bithacks.GetMaskUpToLowestSetBit(0b0111ul));
        Assert.Equal(0b1111ul, Bithacks.GetMaskUpToLowestSetBit(0b1000ul));
        Assert.Equal(0b0011ul, Bithacks.GetMaskUpToLowestSetBit(0b1010ul));
        Assert.Equal(0b0001ul, Bithacks.GetMaskUpToLowestSetBit(0b1011ul));
        Assert.Equal(0b0001ul, Bithacks.GetMaskUpToLowestSetBit(0b1111ul));
        Assert.Equal(0b1111ul, Bithacks.GetMaskUpToLowestSetBit(0b1000ul));
        Assert.Equal((ushort)0b0001, Bithacks.GetMaskUpToLowestSetBit((ushort)0b0001));
        Assert.Equal((ushort)0b0011, Bithacks.GetMaskUpToLowestSetBit((ushort)0b0010));
        Assert.Equal((ushort)0b0001, Bithacks.GetMaskUpToLowestSetBit((ushort)0b0011));
        Assert.Equal((ushort)0b0111, Bithacks.GetMaskUpToLowestSetBit((ushort)0b0100));
        Assert.Equal((ushort)0b0011, Bithacks.GetMaskUpToLowestSetBit((ushort)0b0110));
        Assert.Equal((ushort)0b0001, Bithacks.GetMaskUpToLowestSetBit((ushort)0b0111));
        Assert.Equal((ushort)0b1111, Bithacks.GetMaskUpToLowestSetBit((ushort)0b1000));
        Assert.Equal((ushort)0b0011, Bithacks.GetMaskUpToLowestSetBit((ushort)0b1010));
        Assert.Equal((ushort)0b0001, Bithacks.GetMaskUpToLowestSetBit((ushort)0b1011));
        Assert.Equal((ushort)0b0001, Bithacks.GetMaskUpToLowestSetBit((ushort)0b1111));
        Assert.Equal((ushort)0b1111, Bithacks.GetMaskUpToLowestSetBit((ushort)0b1000));
    }

    [Fact]
    public static void Should_Get_Quote_Mask_Single()
    {
        Assert.Equal(0b00001111u, Bithacks.FindInverseQuoteMaskSingle(0b00010000, 0));
        Assert.Equal(~0b00001111u, Bithacks.FindInverseQuoteMaskSingle(0b00010000, 1));
    }

    [Fact]
    public static void Should_Check_if_Zero_or_One_Bits_Set()
    {
        Assert.True(Bithacks.ZeroOrOneBitsSet(0));
        Assert.True(Bithacks.ZeroOrOneBitsSet(0b00000001u));
        Assert.True(Bithacks.ZeroOrOneBitsSet(0b00000010u));
        Assert.True(Bithacks.ZeroOrOneBitsSet(0b00000000ul));
        Assert.False(Bithacks.ZeroOrOneBitsSet(0b00000011u));
        Assert.False(Bithacks.ZeroOrOneBitsSet(~0u));

        Assert.False(Bithacks.ZeroOrOneBitsSet(0b00000011ul));
        Assert.True(Bithacks.ZeroOrOneBitsSet(0ul));
        Assert.True(Bithacks.ZeroOrOneBitsSet(0b00000010ul));
        Assert.True(Bithacks.ZeroOrOneBitsSet(0b00000001ul));
        Assert.False(Bithacks.ZeroOrOneBitsSet(~0ul));

        Assert.False(Bithacks.ZeroOrOneBitsSet((ushort)0b00000011u));
        Assert.True(Bithacks.ZeroOrOneBitsSet((ushort)0u));
        Assert.True(Bithacks.ZeroOrOneBitsSet((ushort)0b00000010u));
        Assert.True(Bithacks.ZeroOrOneBitsSet((ushort)0b00000001u));
        Assert.False(Bithacks.ZeroOrOneBitsSet((ushort)0xFFFF));

        for (int i = 0; i < 8096; i++)
        {
            Assert.Equal(int.PopCount(i) is 0 or 1, Bithacks.ZeroOrOneBitsSet(i));
        }
    }

    [Fact]
    public static void Should_Check_if_Two_or_More_Bits_Set()
    {
        Assert.False(Bithacks.TwoOrMoreBitsSet(0));
        Assert.False(Bithacks.TwoOrMoreBitsSet(0b00000001u));
        Assert.False(Bithacks.TwoOrMoreBitsSet(0b00000010u));
        Assert.True(Bithacks.TwoOrMoreBitsSet(0b00000011u));
        Assert.True(Bithacks.TwoOrMoreBitsSet(0b00101011u));
        Assert.True(Bithacks.TwoOrMoreBitsSet(~0u));

        Assert.False(Bithacks.TwoOrMoreBitsSet(0ul));
        Assert.False(Bithacks.TwoOrMoreBitsSet(0b00000010ul));
        Assert.False(Bithacks.TwoOrMoreBitsSet(0b00000001ul));
        Assert.False(Bithacks.TwoOrMoreBitsSet(0b00000000ul));
        Assert.True(Bithacks.TwoOrMoreBitsSet(0b00000011ul));
        Assert.True(Bithacks.TwoOrMoreBitsSet(0b00101011ul));
        Assert.True(Bithacks.TwoOrMoreBitsSet(~0ul));

        Assert.False(Bithacks.TwoOrMoreBitsSet((ushort)0));
        Assert.False(Bithacks.TwoOrMoreBitsSet((ushort)0b00000010));
        Assert.False(Bithacks.TwoOrMoreBitsSet((ushort)0b00000001));
        Assert.False(Bithacks.TwoOrMoreBitsSet((ushort)0b00000000));
        Assert.True(Bithacks.TwoOrMoreBitsSet((ushort)0b00000011));
        Assert.True(Bithacks.TwoOrMoreBitsSet((ushort)0b00101011));
        Assert.True(Bithacks.TwoOrMoreBitsSet((ushort)0xFFFF));

        for (int i = 0; i < 8096; i++)
        {
            Assert.Equal(int.PopCount(i) >= 2, Bithacks.TwoOrMoreBitsSet(i));
            Assert.Equal(int.PopCount(i) >= 2, Bithacks.TwoOrMoreBitsSet((long)i));
        }
    }

    public static TheoryData<ulong, ulong> TestData =>
        new()
        {
            { 0, 0 },
            { 0b1, ulong.MaxValue },
            { 0b00100000010ul, 0b0011111110ul },
            { 0b1001, 0b0111 },
            { 0b10001, 0b01111 },
            { 0b100001, 0b011111 },
            { 0b1000001, 0b0111111 },
            { 0b10000001, 0b01111111 },
            { 0b100000001, 0b011111111 },
            { 0b1000000001, 0b0111111111 },
            { 0b10000000001, 0b01111111111 },
            { 0b100000000001, 0b011111111111 },
            { 0b1000000000001, 0b0111111111111 },
            { 0b10000000000001, 0b01111111111111 },
            { 0b100000000000001, 0b011111111111111 },
            { 0b1000000000000001, 0b0111111111111111 },
            { 0b10000000000000001, 0b01111111111111111 },
            { 0b100000000000000001, 0b011111111111111111 },
            { 0b1000000000000000001, 0b0111111111111111111 },
            { 0b10000000000000000001, 0b01111111111111111111 },
            { 0b100000000000000000001, 0b011111111111111111111 },
            { 0b1000000000000000000001, 0b0111111111111111111111 },
            { 0b10000000000000000000001, 0b01111111111111111111111 },
            { 0b100000000000000000000001, 0b011111111111111111111111 },
        };

    public static IEnumerable<TheoryDataRow<uint, uint>> TestData32 =>
        TestData
            .Where(x => x.Data is { Item1: <= uint.MaxValue, Item2: <= uint.MaxValue })
            .Select(x => new TheoryDataRow<uint, uint>(checked((uint)x.Data.Item1), checked((uint)x.Data.Item2)));

    [Theory]
    [MemberData(nameof(TestData))]
    public static void QuoteMaskTests64(ulong input, ulong expected)
    {
        var actual = Bithacks.ComputeQuoteMask(input);
        Assert.Equal(expected.ToString("b64"), actual.ToString("b64"));

        actual = Bithacks.ComputeQuoteMaskSoftwareFallback(input);
        Assert.Equal(expected.ToString("b64"), actual.ToString("b64"));
    }

    [Theory]
    [MemberData(nameof(TestData32))]
    public static void QuoteMaskTests32(uint input, uint expected)
    {
        var actual = Bithacks.ComputeQuoteMask(input);
        Assert.Equal(expected.ToString("b32"), actual.ToString("b32"));

        actual = Bithacks.ComputeQuoteMaskSoftwareFallback(input);
        Assert.Equal(expected.ToString("b32"), actual.ToString("b32"));
    }

    [Fact]
    public static void FindQuotes32() => FindQuotesImpl<uint>();

    [Fact]
    public static void FindQuotes64() => FindQuotesImpl<ulong>();

    private static void FindQuotesImpl<TMask>()
        where TMask : unmanaged, IBinaryInteger<TMask>, IUnsignedNumber<TMask>
    {
        const string data =
            "The quick 'brown, fox' jumps, over the dog "
            + "The quick, brown fox 'jumps over' the dog "
            + "The 'quick brn fox ''jumps'' over the,'lazy";

        Assert.Equal(128, data.Length);
        Assert.Equal(0, data.Length % Unsafe.SizeOf<TMask>());

        int[] expectedIndexes = data.AsEnumerable().Index().Where(x => x.Item == '\'').Select(x => x.Index).ToArray();
        const int expectedCommas = 2;

        TMask carry = TMask.Zero;
        TMask commaCount = TMask.Zero;

        List<int> indexes = [];
        uint count = 0;

        for (int i = 0; i < data.Length; i += (8 * Unsafe.SizeOf<TMask>()))
        {
            TMask quoteBits = LoadBits('\'', i);
            TMask quoteMask = Unsafe.SizeOf<TMask>() switch
            {
                4 => (TMask)(object)Bithacks.ComputeQuoteMask((uint)(object)(quoteBits) + count),
                8 => (TMask)(object)Bithacks.ComputeQuoteMask((ulong)(object)(quoteBits) + count),
                _ => throw new NotSupportedException(),
            };

            TMask commaBits = LoadBits(',', i);
            commaBits &= ~quoteMask;
            commaCount += TMask.PopCount(commaBits);

            TMask current = quoteBits;

            while (current != TMask.Zero)
            {
                int offset = int.CreateChecked(TMask.TrailingZeroCount(current));
                indexes.Add(i + offset);
                current &= current - TMask.One;
                count = uint.CreateChecked(TMask.PopCount(quoteMask >> offset));
            }

            if (Debugger.IsAttached)
            {
                // ReSharper disable UnusedVariable
                string fmt = $"B{8 * Unsafe.SizeOf<TMask>()}";
                string strCurrent = data.AsSpan(i, 8 * Unsafe.SizeOf<TMask>()).ToString();
                string strQuoteBits = quoteBits.ToString(fmt, null);
                string strQuoteMask = quoteMask.ToString(fmt, null);
                string strCommaBitsBefore = LoadBits(',', i).ToString(fmt, null);
                string strCommaBitsAfter = commaBits.ToString(fmt, null);
                _ = 1;
                // ReSharper restore UnusedVariable
            }
        }

        Assert.Equal(expectedIndexes, indexes);
        Assert.Equal(expectedCommas, int.CreateChecked(commaCount));
        Assert.Equal(TMask.Zero, carry);

        static TMask LoadBits(char needle, int start)
        {
            TMask value = TMask.Zero;

            for (int i = 0; i < 8 * Unsafe.SizeOf<TMask>(); i++)
            {
                if (data[i + start] == needle)
                {
                    value |= TMask.One << i;
                }
            }

            return value;
        }
    }
}
