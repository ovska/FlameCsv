using System.Runtime.InteropServices;
using FlameCsv.Exceptions;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Tests.Readers;

public static class MetaTests
{
    public sealed class MetaTheoryData : TheoryData<int, uint, uint, bool>;

    public static MetaTheoryData MetaData /* heh */
        => new()
        {
            { 0, 0, 0, false },
            { 5, 0, 0, false },
            { 0, 2, 1, false },
            { 0, 2, 0, false },
            { 0, 2, 2, false },
            { 5, 2, 2, false },
            { 0, 0, 0, true },
            { 5, 0, 0, true },
            { 0, 2, 0, true },
            { 0, 2, 1, true },
            { 0, 2, 2, true },
            { 5, 2, 2, true },
        };

    [Theory]
    [MemberData(nameof(MetaData))]
    public static void Should_Handle_Special_And_Flags(int end, uint quoteCount, uint escapeCount, bool isEOL)
    {
        Meta meta = escapeCount == 0
            ? Meta.RFC(end, quoteCount, isEOL, 2)
            : Meta.Unix(end, quoteCount, escapeCount, isEOL, 2);

        Assert.Equal(end, meta.End);
        Assert.Equal(isEOL, meta.IsEOL);

        if (escapeCount == 0)
        {
            Assert.False(meta.IsEscape);
            Assert.Equal(quoteCount, meta.SpecialCount);
        }
        else
        {
            Assert.True(meta.IsEscape);
            Assert.Equal(escapeCount, meta.SpecialCount);
        }

        if (isEOL)
        {
            Assert.Equal(meta.End + 2, meta.NextStart);
        }
        else
        {
            Assert.Equal(meta.End + 1, meta.NextStart);
        }
    }

    public static TheoryData<uint, uint> InvalidMetaData
        => new()
        {
            { 0, 1 },
            { 0, 3 },
            { 1, 0 },
            { 1, 1 },
            { 1, 2 },
            { 3, 0 },
            { 3, 2 },
            { 3, 3 },
        };

    [Theory]
    [MemberData(nameof(InvalidMetaData))]
    public static void Should_Validate_Args(uint quoteCount, uint escapeCount)
    {
        Assert.Throws<CsvFormatException>(
            () =>
            {
                _ = escapeCount == 0
                    ? Meta.RFC(0, quoteCount, isEOL: false, newlineLength: 2)
                    : Meta.Unix(0, quoteCount, escapeCount, false, newlineLength: 2);
            });
    }

    [Fact]
    public static void Should_Handle_Start_of_Data()
    {
        Assert.Equal(0u, Meta.StartOfData.SpecialCount);
        Assert.False(Meta.StartOfData.IsEscape);
        Assert.False(Meta.StartOfData.IsEOL);
        Assert.Equal(0, Meta.StartOfData.End);
        Assert.Equal(0, Meta.StartOfData.NextStart);
    }

    [Theory]
    [InlineData(0, false, 2, 1)]
    [InlineData(0, true, 1, 1)]
    [InlineData(0, true, 2, 2)]
    [InlineData(572, false, 2, 573)]
    [InlineData(572, true, 1, 573)]
    [InlineData(572, true, 2, 574)]
    public static void Should_Get_Start_Of_Next(int end, bool isEOL, int newlineLength, int expected)
    {
        var meta = Meta.RFC(end, 0, isEOL, newlineLength);
        Assert.Equal(expected, meta.NextStart);
    }

    [Fact]
    public static void Should_Check_SpecialCount()
    {
        const int max = 0x3FFF_FFFF;

        Assert.Throws<NotSupportedException>(() => Meta.RFC(0, max + 1, true, 2));
        Assert.Throws<NotSupportedException>(() => Meta.RFC(0, max + 1, false, 2));
        Assert.Throws<NotSupportedException>(() => Meta.Unix(0, 2, max + 1, false, 2));
        Assert.Throws<NotSupportedException>(() => Meta.Unix(0, 2, max + 1, true, 2));
    }

    [Fact]
    public static void Should_Have_IsEscape_Off_If_No_Escapes()
    {
        var meta = Meta.Unix(5, 2, 0, isEOL: false, 1);
        Assert.False(meta.IsEscape); // unix-style meta but no escapes
    }

    [Fact]
    public static void Should_Slice()
    {
        const string data = "abc,def,ghi,jkl,mno\r\npqr,stu,vwx,yz\r\n";
        Span<Meta> metaBuffer =
        [
            Meta.Plain(3),
            Meta.Plain(7),
            Meta.Plain(11),
            Meta.Plain(15),
            Meta.Plain(19, isEOL: true, 2),
            Meta.Plain(24),
            Meta.Plain(28),
            Meta.Plain(32),
            Meta.Plain(35, isEOL: true, 2),
        ];

        int start = 0;

        string[] expected = ["abc", "def", "ghi", "jkl", "mno", "pqr", "stu", "vwx", "yz"];

        for (int i = 0; i < metaBuffer.Length; i++)
        {
            Meta meta = metaBuffer[i];

            ReadOnlySpan<char> field = meta.GetField(
                in CsvOptions<char>.Default.Dialect,
                start,
                ref MemoryMarshal.GetReference(data.AsSpan()),
                [],
                null!);
            Assert.Equal(expected[i], field.ToString());

            start = meta.NextStart;
        }

        Assert.Equal(data.Length, start);
    }

    [Theory]
    [MemberData(nameof(NewlineData))]
    public static void Should_Find_Newline(bool[] values, int expected)
    {
        var metas = values.Select(static b => Meta.Plain(0, isEOL: b, 2)).ToArray();

        ref Meta first = ref metas[0];

        if (expected == -1)
        {
            Assert.False(Meta.TryFindNextEOL(ref first, metas.Length, out _));
        }
        else
        {
            Assert.True(Meta.TryFindNextEOL(ref first, metas.Length, out int index));
            Assert.Equal(expected, index);
        }
    }

    public static TheoryData<bool[], int> NewlineData
        => new()
        {
            { [true], 1 },
            { [true, false], 1 },
            { [false, true], 2 },
            { [false, false, true], 3 },
            { [false, false, false], -1 },
            { R(false, 16).Concat(R(true, 1)).ToArray(), 17 },
            { R(false, 15).Concat(R(true, 15)).ToArray(), 16 },
            { R(false, 16).ToArray(), -1 },
            { [..R(false, 5), true, ..R(false, 128)], 6 },
            { [..R(false, 32), true, ..R(false, 128)], 33 },
            { [..R(false, 129), true, ..R(false, 128)], 130 },
        };

    private static IEnumerable<bool> R(bool value, int count) => Enumerable.Repeat(value, count);
}
