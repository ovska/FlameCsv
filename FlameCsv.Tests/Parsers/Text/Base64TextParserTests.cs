using System.Text;
using FlameCsv.Parsers.Text;

namespace FlameCsv.Tests.Parsers.Text;

public static class Base64TextParserTests
{
    private const string input = "VGhlIHF1aWNrIGJyb3duIGZveCBqdW1wcyBvdmVyIHRoZSBsYXp5IGRvZy4=";
    private static readonly Base64TextParser _parser = new();

    [Fact]
    public static void Should_Implement_CanParse()
    {
        Assert.True(_parser.CanParse(typeof(byte[])));
        Assert.True(_parser.CanParse(typeof(Memory<byte>)));
        Assert.True(_parser.CanParse(typeof(ReadOnlyMemory<byte>)));
        Assert.True(_parser.CanParse(typeof(ArraySegment<byte>)));
        Assert.False(_parser.CanParse(typeof(int[])));
    }

    [Fact]
    public static void Should_Parse_Empty()
    {
        Assert.True(_parser.TryParse("", out byte[] bytes));
        Assert.Empty(bytes);
    }

    [Fact]
    public static void Should_Parse_Utf8_To_Array()
    {
        Assert.True(_parser.TryParse(input, out byte[] bytes));
        AssertValid(bytes);
    }

    [Fact]
    public static void Should_Parse_Utf8_To_ArraySegment()
    {
        Assert.True(_parser.TryParse(input, out ArraySegment<byte> bytes));
        AssertValid(bytes);
    }

    [Fact]
    public static void Should_Parse_Utf8_To_Memory()
    {
        Assert.True(_parser.TryParse(input, out Memory<byte> bytes));
        AssertValid(bytes.Span);
    }

    [Fact]
    public static void Should_Parse_Utf8_To_ReadOnlyMemory()
    {
        Assert.True(_parser.TryParse(input, out ReadOnlyMemory<byte> bytes));
        AssertValid(bytes.Span);
    }

    [Fact]
    public static void Should_Parse_Long()
    {
        // test input requires buffer over 256 long
        const string b64
            = "QUJDREVGR0hJSktMTU5PUFFSU1RVVldYWVoKYWJjZGVmZ2hpamtsbW5vcHFyc3R1dnd4"
            + "eXoKMDEyMzQ1Njc4OQpBQkNERUZHSElKS0xNTk9QUVJTVFVWV1hZWgphYmNkZWZnaGlq"
            + "a2xtbm9wcXJzdHV2d3h5egowMTIzNDU2Nzg5CkFCQ0RFRkdISUpLTE1OT1BRUlNUVVZX"
            + "WFlaCmFiY2RlZmdoaWprbG1ub3BxcnN0dXZ3eHl6CjAxMjM0NTY3ODk=";

        const string expected =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ\nabcdefghijklmnopqrstuvwxyz\n0123456789"
            + "\nABCDEFGHIJKLMNOPQRSTUVWXYZ\nabcdefghijklmnopqrstuvwxyz\n0123456789"
            + "\nABCDEFGHIJKLMNOPQRSTUVWXYZ\nabcdefghijklmnopqrstuvwxyz\n0123456789";

        Assert.True(_parser.TryParse(b64, out byte[] bytes));
        Assert.Equal(expected, Encoding.UTF8.GetString(bytes));
    }

    private static void AssertValid(ReadOnlySpan<byte> data)
    {
        Assert.Equal(
            "The quick brown fox jumps over the lazy dog.",
            Encoding.UTF8.GetString(data));
    }
}
