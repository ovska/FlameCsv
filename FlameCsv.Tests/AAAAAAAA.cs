using FlameCsv.Reading.Internal;

namespace FlameCsv.Tests;

public class AAAAAAAA
{
    [Theory]
    [InlineData("te\"\"st", "te\"st")]
    [InlineData("test\"\"", "test\"")]
    [InlineData("\"\"test\"\"", "\"test\"")]
    [InlineData("\"\"", "\"")]
    [InlineData(
        "The one and only, James \"\"007\"\" Bond, in Her Majesty's Secret Service",
        "The one and only, James \"007\" Bond, in Her Majesty's Secret Service")]
    [InlineData(
        "GBC Pre-Punched Binding Paper, Plastic, White, 8-1/2\"\" x 11\"\"",
        "GBC Pre-Punched Binding Paper, Plastic, White, 8-1/2\" x 11\"")]
    [InlineData("Exactly \"\"32\"\" characters input!", "Exactly \"32\" characters input!")]
    public void AAAAA(string inp, string expected)
    {
        var buffer = new char[128];
        int quoteCount = inp.AsSpan().Count('"');
        int expectedLength = inp.Length - quoteCount / 2;
        Unesc.FillBitmask('"', (uint)quoteCount, inp, buffer);
        Assert.Equal(expected, buffer.AsSpan(..expectedLength).ToString());
        Assert.Equal(new string('\0', 128 - expectedLength), buffer.AsSpan(expectedLength..).ToString());
    }
}
