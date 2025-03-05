using System.Text;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Tests;

public class AAAAAAAA
{
    public static TheoryData<string, string> TestData
        => new()
        {
            { "te\"\"st", "te\"st" },
            { "test\"\"", "test\"" },
            { "\"\"test\"\"", "\"test\"" },
            { "\"\"", "\"" },
            {
                "The one and only, James \"\"007\"\" Bond, in Her Majesty's Secret Service",
                "The one and only, James \"007\" Bond, in Her Majesty's Secret Service"
            },
            {
                "GBC Pre-Punched Binding Paper, Plastic, White, 8-1/2\"\" x 11\"\"",
                "GBC Pre-Punched Binding Paper, Plastic, White, 8-1/2\" x 11\""
            },
            { "Exactly \"\"32\"\" characters input!", "Exactly \"32\" characters input!" },
            { "Wilson Jones 1\"\" Hanging DublLock® Ring Binders", "Wilson Jones 1\" Hanging DublLock® Ring Binders" },
            {
                "Wilson Jones 1\"\" Hanging DublLock® Ring Binders!", "Wilson Jones 1\" Hanging DublLock® Ring Binders!"
            },
            { "Wilson Jones 11\"\" Hanging DublLock® Ring Binder", "Wilson Jones 11\" Hanging DublLock® Ring Binder" },
            { "\"\"\"\"\"\"\"\"\"\"\"\"\"\"\"\"", "\"\"\"\"\"\"\"\"" }
        };

    [Theory]
    [MemberData(nameof(TestData))]
    public void Unescape_Char(string inp, string expected)
    {
        int quoteCount = inp.AsSpan().Count('"');
        int expectedLength = inp.Length - quoteCount / 2;
        var buffer = new char[128];
        Unesc.Exec('"', quoteCount, inp, buffer);
        Assert.Equal(expected, buffer.AsSpan(..expectedLength).ToString());
        Assert.Equal(new string('\0', 128 - expectedLength), buffer.AsSpan(expectedLength..).ToString());
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public void Unescape_Byte(string inp, string expected)
    {
        int quoteCount = inp.AsSpan().Count('"');
        int expectedLength = inp.Length - quoteCount / 2;
        var byteBuffer = new byte[128];
        Unesc.Exec((byte)'"', quoteCount, Encoding.UTF8.GetBytes(inp), byteBuffer);
        Assert.Equal(expected, Encoding.UTF8.GetString(byteBuffer.AsSpan(..expectedLength)));
        Assert.Equal(new byte[128 - expectedLength], byteBuffer.AsSpan(expectedLength..));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0b0011, 0b0001)]
    [InlineData(0b0110, 0b0010)]
    [InlineData(0b1100, 0b0100)]
    [InlineData(0b1111, 0b0101)]
    [InlineData(0b11110, 0b01010)]
    [InlineData(0b111111, 0b010101)]
    [InlineData(
        0b11001100,
        0b01000100)]
    [InlineData(0b11000000000000000000000000000000,0b01000000000000000000000000000000)]
    // unpaired
    [InlineData(0b1,0)]
    [InlineData(0b10000000000000000000000000000000,0)]
    public void ConvertBitmask(uint input, uint expected)
    {
        Assert.Equal(expected.ToString("b8"), Unesc.ConvertBitmask(input).ToString("b8"));
    }
}
