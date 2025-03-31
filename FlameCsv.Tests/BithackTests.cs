using FlameCsv.Reading.Internal;

namespace FlameCsv.Tests;

public class BithackTests
{
    public static TheoryData<ulong, ulong> TestData
        => new()
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

    public static IEnumerable<TheoryDataRow<uint, uint>> TestData32
        => TestData
            .Where(x => x.Data is { Item1: <= uint.MaxValue, Item2: <= uint.MaxValue })
            .Select(x => new TheoryDataRow<uint, uint>(checked((uint)x.Data.Item1), checked((uint)x.Data.Item2)))
            .ToArray();

    [Theory]
    [MemberData(nameof(TestData))]
    public static void QuoteMaskTests64(ulong input, ulong expected)
    {
        var actual = Bithacks.ComputeQuoteMask((nuint)input);
        Assert.Equal(expected.ToString("b64"), actual.ToString("b64"));
    }

    [Theory]
    [MemberData(nameof(TestData32))]
    public static void QuoteMaskTests32(uint input, uint expected)
    {
        var actual = Bithacks.ComputeQuoteMask((nuint)input);
        Assert.Equal(expected.ToString("b32"), actual.ToString("b32"));
    }
}
