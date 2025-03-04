using System.Text;
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
    // quotes near vector boundaries
    [InlineData("Wilson Jones 1\"\" Hanging DublLock® Ring Binders", "Wilson Jones 1\" Hanging DublLock® Ring Binders")]
    [InlineData("Wilson Jones 1\"\" Hanging DublLock® Ring Binders!", "Wilson Jones 1\" Hanging DublLock® Ring Binders!")]
    [InlineData("Wilson Jones 11\"\" Hanging DublLock® Ring Binder", "Wilson Jones 11\" Hanging DublLock® Ring Binder")]
    public void AAAAA(string inp, string expected)
    {
        var buffer = new char[128];
        int quoteCount = inp.AsSpan().Count('"');
        int expectedLength = inp.Length - quoteCount / 2;
        Unesc.Exec('"', quoteCount, inp, buffer);
        Assert.Equal(expected, buffer.AsSpan(..expectedLength).ToString());
        Assert.Equal(new string('\0', 128 - expectedLength), buffer.AsSpan(expectedLength..).ToString());

        var byteBuffer = new byte[128];
        Unesc.Exec((byte)'"', quoteCount, Encoding.UTF8.GetBytes(inp), byteBuffer);
        Assert.Equal(expected, Encoding.UTF8.GetString(byteBuffer.AsSpan(..expectedLength)));
        Assert.Equal(new byte[128 - expectedLength], byteBuffer.AsSpan(expectedLength..));
    }
}
