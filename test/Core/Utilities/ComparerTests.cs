using System.Text;
using FlameCsv.Reflection;
using FlameCsv.Utilities.Comparers;

namespace FlameCsv.Tests.Utilities;

public static class ComparerTests
{
    [Theory]
    [MemberData(nameof(AsciiData))]
    public static void Should_Compare_Ascii(string left, string right)
    {
        Assert.True(Ascii.IsValid(left));
        Assert.True(Ascii.IsValid(right));

        byte[] leftBytes = Encoding.UTF8.GetBytes(left);
        byte[] rightBytes = Encoding.UTF8.GetBytes(right);

        Test(
            StringComparer.Ordinal.Equals(left, right),
            OrdinalAsciiComparer.Instance,
            left,
            right,
            leftBytes,
            rightBytes);

        Test(
            StringComparer.OrdinalIgnoreCase.Equals(left, right),
            IgnoreCaseAsciiComparer.Instance,
            left,
            right,
            leftBytes,
            rightBytes);
    }

    [Theory]
    [MemberData(nameof(Utf8Data))]
    public static void Should_Compare_Utf8(string left, string right)
    {
        byte[] leftBytes = Encoding.UTF8.GetBytes(left);
        byte[] rightBytes = Encoding.UTF8.GetBytes(right);

        Test(
            StringComparer.Ordinal.Equals(left, right),
            Utf8Comparer.Ordinal,
            left,
            right,
            leftBytes,
            rightBytes);

        Test(
            StringComparer.OrdinalIgnoreCase.Equals(left, right),
            Utf8Comparer.OrdinalIgnoreCase,
            left,
            right,
            leftBytes,
            rightBytes);
    }

    private static void Test(
        bool expected,
        IEqualityComparer<string> cmp,
        string left,
        string right,
        byte[] leftBytes,
        byte[] rightBytes)
    {
        var altCmp = (IAlternateEqualityComparer<ReadOnlySpan<byte>, string>)cmp;

        Assert.Equal(expected, cmp.Equals(left, right));
        Assert.Equal(expected, altCmp.Equals(leftBytes, right));
        Assert.Equal(expected, cmp.Equals(right, left));
        Assert.Equal(expected, altCmp.Equals(rightBytes, left));
        Assert.Equal(expected, cmp.GetHashCode(left) == cmp.GetHashCode(right));
        Assert.Equal(expected, altCmp.GetHashCode(leftBytes) == cmp.GetHashCode(right));
        Assert.Equal(expected, cmp.GetHashCode(right) == cmp.GetHashCode(left));
        Assert.Equal(expected, altCmp.GetHashCode(rightBytes) == cmp.GetHashCode(left));
        Assert.Equal(expected, altCmp.GetHashCode(leftBytes) == altCmp.GetHashCode(rightBytes));

        var slc = (IEqualityComparer<StringLike>)cmp;
        Assert.Equal(expected, slc.Equals(left, right));
        Assert.Equal(expected, slc.GetHashCode(right) == slc.GetHashCode(left));

        var altSlc = (IAlternateEqualityComparer<ReadOnlySpan<byte>, StringLike>)cmp;
        Assert.Equal(expected, altSlc.GetHashCode(leftBytes) == cmp.GetHashCode(right));
        Assert.Equal(expected, cmp.GetHashCode(right) == cmp.GetHashCode(left));
        Assert.Equal(expected, altSlc.GetHashCode(rightBytes) == cmp.GetHashCode(left));
        Assert.Equal(expected, altSlc.GetHashCode(leftBytes) == altSlc.GetHashCode(rightBytes));

        Assert.False(cmp.Equals(null, ""));
        Assert.False(cmp.Equals("", null));
        Assert.True(cmp.Equals(null, null));

        Assert.Equal(left, altCmp.Create(leftBytes));
        Assert.Equal(right, altCmp.Create(rightBytes));
    }

    public static TheoryData<string, string> AsciiData
        => new()
        {
            { "", "" },
            { "", "a" },
            { "a", "" },
            { "a", "a" },
            { "A", "a" },
            { "a", "A" },
            { "A", "A" },
            { "abc", "abc" },
            { "ABC", "abc" },
            { "abc", "ABC" },
            { "ABC", "ABC" },
            { "aBc", "abC" },
            { "Hello", "hello" },
            { "WORLD", "world" },
            { "Test", "Testing" },
            { "Testing", "Test" },
            { "  leading", "leading" },
            { "trailing  ", "trailing" },
            { "  both  ", "both" },
            { "123", "123" },
            { "123", "1234" },
            { "1234", "123" },
            { "!@#", "!@#" },
            { "Test1", "test1" },
            { "Test!", "test!" },
            { "abcdefghijklmnopqrstuvwxyz", "ABCDEFGHIJKLMNOPQRSTUVWXYZ" },
            { "ABCDEFGHIJKLMNOPQRSTUVWXYZ", "abcdefghijklmnopqrstuvwxyz" },
            { "The quick brown fox", "THE QUICK BROWN FOX" },
            { "The quick brown fox jumps over the lazy dog", "The quick brown fox jumps over the lazy dog" },
            { "The quick brown fox jumps over the lazy dog", "THE QUICK BROWN FOX JUMPS OVER THE LAZY DOG" },
            { "alpha", "beta" },
            { "Alpha", "beta" },
            { "alpha", "Beta" },
            { "Alpha", "Beta" },
            { "string1", "string2" },
            { "prefix", "prefixSuffix" },
            { "prefixSuffix", "prefix" },
            { "commonPrefixA", "commonPrefixB" },
            { "aSuffix", "bSuffix" },
            { "MiXeD", "mIxEd" },
            { "Spaces In Between", "spaces in between" },
            { "    ", "    " }, // only spaces
            { "    ", "   " }, // different number of spaces
            { "\t", "\t" }, // tabs
            { "\n", "\n" }, // newlines
            { "\r\n", "\r\n" }, // CRLF
            { "Test\0Null", "Test\0Null" }, // with null char
            { "Test\0NullA", "Test\0NullB" }, // with null char, different suffix
            { "Test\0Null", "test\0null" }, // with null char, different case
        };

    public static TheoryData<string, string> Utf8Data
    {
        get
        {
            var combined = new TheoryData<string, string>
            {
                { "Hello, 世界", "hello, 世界" },
                { "こんにちは", "Kon'nichiwa" },
                { "Привет", "привет" },
                { "Γειά σου", "γειά σου" },
                { "नमस्ते", "नमस्ते" },
                { "مرحبا", "مرحبا" },
                { "안녕하세요", "안녕하세요" },
                { "你好", "你好" },
                { "שלום", "שלום" },
            };
            combined.AddRange(AsciiData);
            return combined;
        }
    }
}
