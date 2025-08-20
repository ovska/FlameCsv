using System.Diagnostics;
using System.Runtime.CompilerServices;
using FlameCsv.Intrinsics;

namespace FlameCsv.Tests.Reading;

public class BithackTests
{
    [Fact]
    public static void Should_Check_if_Zero_or_One_Bits_Set()
    {
        Assert.True(Bithacks.ZeroOrOneBitsSet(0));
        Assert.True(Bithacks.ZeroOrOneBitsSet(0ul));
        Assert.True(Bithacks.ZeroOrOneBitsSet(0b00000001u));
        Assert.True(Bithacks.ZeroOrOneBitsSet(0b00000010u));
        Assert.True(Bithacks.ZeroOrOneBitsSet(0b00000010ul));
        Assert.True(Bithacks.ZeroOrOneBitsSet(0b00000001ul));
        Assert.True(Bithacks.ZeroOrOneBitsSet(0b00000000ul));
        Assert.False(Bithacks.ZeroOrOneBitsSet(0b00000011u));
        Assert.False(Bithacks.ZeroOrOneBitsSet(0b00000011ul));
        Assert.False(Bithacks.ZeroOrOneBitsSet(~0u));
        Assert.False(Bithacks.ZeroOrOneBitsSet(~0ul));

        for (int i = 0; i < 8096; i++)
        {
            Assert.Equal(int.PopCount(i) is 0 or 1, Bithacks.ZeroOrOneBitsSet(i));
        }
    }
 
    [Fact]
    public static void Should_Check_If_All_Bits_Before()
    {
        Assert.True(Bithacks.AllBitsBefore(0b0000000000000001, 0b0000000000000010));
        Assert.True(Bithacks.AllBitsBefore(0b0000000000000010, 0b0000000000000100));
        Assert.False(Bithacks.AllBitsBefore(0b0000000000000010, 0b0000000100000010));
        Assert.False(Bithacks.AllBitsBefore(0b10000000, 0b01010101));
        Assert.True(Bithacks.AllBitsBefore(0b00_01000000, 0b01010101_10000000));
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
            .Select(x => new TheoryDataRow<uint, uint>(checked((uint)x.Data.Item1), checked((uint)x.Data.Item2)))
            .ToArray();

    public static IEnumerable<TheoryDataRow<ushort, ushort>> TestData16 =>
        TestData
            .Where(x => x.Data is { Item1: <= ushort.MaxValue, Item2: <= ushort.MaxValue })
            .Select(x => new TheoryDataRow<ushort, ushort>(
                checked((ushort)x.Data.Item1),
                checked((ushort)x.Data.Item2)
            ))
            .ToArray();

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

    [Theory]
    [MemberData(nameof(TestData16))]
    public static void QuoteMaskTests16(ushort input, ushort expected)
    {
        var actual = Bithacks.ComputeQuoteMask(input);
        Assert.Equal(expected.ToString("b16"), actual.ToString("b16"));

        actual = Bithacks.ComputeQuoteMaskSoftwareFallback(input);
        Assert.Equal(expected.ToString("b16"), actual.ToString("b16"));
    }

    [Fact]
    public static void FindQuotes16() => FindQuotesImpl<ushort>();

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
            TMask quoteMask = Bithacks.FindQuoteMask(quoteBits, count);

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
