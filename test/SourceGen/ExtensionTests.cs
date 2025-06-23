using FlameCsv.SourceGen.Helpers;
using FlameCsv.SourceGen.Utilities;

namespace FlameCsv.Tests.SourceGen;

public static class ExtensionTests
{
    [Theory]
    [InlineData(false, "abc")]
    [InlineData(false, "ABC")]
    [InlineData(false, "123abc")]
    [InlineData(true, "123")]
    [InlineData(true, " ")]
    [InlineData(true, "🐉")]
    public static void Test_IsCaseAgnostic(bool expected, string value)
    {
        Assert.Equal(expected, value.IsCaseAgnostic());
    }

    [Theory]
    [InlineData(null, "default")]
    [InlineData(1, "1")]
    [InlineData("abc", "\"abc\"")]
    [InlineData('a', "'a'")]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    [InlineData("", "\"\"")]
    [InlineData("test\\a", "\"test\\\\a\"")]
    [InlineData(-1, "-1")]
    public static void Test_ToLiteral(object? value, string expected)
    {
        Assert.Equal(expected, value.ToLiteral());
    }

    [Theory]
    [InlineData(null, "null")]
    [InlineData("", "\"\"")]
    [InlineData("abc", "\"abc\"")]
    [InlineData("test\\a", "\"test\\\\a\"")]
    public static void Test_ToStringLiteral(string? value, string expected)
    {
        Assert.Equal(expected, value.ToStringLiteral());
    }

    [Fact]
    public static void Test_EquatableArray()
    {
        Assert.Equal(EquatableArray<int>.Empty, EquatableArray<int>.Empty);
        Assert.Equal(EquatableArray<int>.Empty, new EquatableArray<int>());
        Assert.Equal(EquatableArray<int>.Empty, []);
        Assert.Equal(EquatableArray<int>.Empty, new EquatableArray<int>(Array.Empty<int>()));

        Assert.NotEqual(EquatableArray<int>.Empty, EquatableArray.Create([1]));
        Assert.Equal(new EquatableArray<int>([1]), new EquatableArray<int>([1]));
        Assert.NotEqual(new EquatableArray<int>([1]), new EquatableArray<int>([2]));

        Assert.Equal(new EquatableArray<int>([1, 2]), new EquatableArray<int>([1, 2]));
        Assert.NotEqual(new EquatableArray<int>([1, 2]), new EquatableArray<int>([1, 3]));

        Assert.Equal(
            new EquatableArray<(string name, string display)>([("x", "y"), ("a", "b")]),
            new EquatableArray<(string name, string display)>([("x", "y"), ("a", "b")])
        );

        // ReSharper disable once RedundantCast
        Assert.Equal(EquatableArray.Create(1, 2, 3), new EquatableArray<int>((ReadOnlySpan<int>)[1, 2, 3]));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("abc", true)]
    [InlineData("test\\a", true)]
    [InlineData("test value", true)]
    [InlineData("Hello, World!", true)]
    [InlineData("The quick 'brown' fox, jumps over the _lazy_ d^g", true)]
    [InlineData("Café", false)]
    [InlineData("Non-ASCII: ñ", false)]
    [InlineData("Another test: é", false)]
    [InlineData("Märkä Mörkö", false)]
    [InlineData("Unicode: 𝄞", false)]
    [InlineData(
        "Very long value with a lot of characters: 1234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ!@#$%^&*()_+[]{}|;':\",.<>?/~`",
        true
    )]
    [InlineData(
        "Very long value with a lot of characters: 1234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ!@#$%^&*()_+[]{}|;':\",.<>?/~`Café",
        false
    )]
    [InlineData(
        "Very long value with a lot of characters: 1234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ!@#$%^&*()_+[]{}|;':`e,.<>?/~`",
        true
    )]
    [InlineData(
        "Very long value with a lot of characters: Café 1234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ!@#$%^&*()_+[]{}|;':\",.<>?/~`",
        false
    )]
    [InlineData("Another long string with special characters: ~!@#$%^&*()_+", true)]
    [InlineData("Special characters: ©, ™, ®", false)]
    public static void Test_IsAscii(string? value, bool expected)
    {
        Assert.Equal(expected, System.Text.Ascii.IsValid(value));
        Assert.Equal(expected, value.IsAscii());
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("abc", false)]
    [InlineData("test\\a", false)]
    [InlineData("test value", false)]
    [InlineData("Hello, World!", false)]
    [InlineData("The quick 'brown' fox, jumps over the _lazy_ d^g", false)]
    [InlineData("Café", false)]
    [InlineData("Non-ASCII: ñ", false)]
    [InlineData("Another test: é", false)]
    [InlineData("Märkä Mörkö", false)]
    [InlineData("Unicode: 𝄞", true)]
    [InlineData("aaaa🦄aaaaa", true)]
    [InlineData("aaaa🦄aaaaa🦄", true)]
    [InlineData("🥩", true)]
    [InlineData("🥩🥩", true)]
    [InlineData("🥩🥩🥩", true)]
    [InlineData("🥩🥩🥩🥩🥩🥩🥩🥩", true)]
    public static void Test_ContainsSurrogates(string value, bool expected)
    {
        Assert.Equal(expected, value.ContainsSurrogates());

        if (!expected)
        {
            var lowerInvariant = value.ToLowerInvariant();
            var upperInvariant = value.ToUpperInvariant();

            Assert.Equal(value.Length, lowerInvariant.Length);
            Assert.Equal(value.Length, upperInvariant.Length);

            for (int i = 0; i < value.Length; i++)
            {
                Assert.Equal(char.ToLowerInvariant(value[i]), lowerInvariant[i]);
                Assert.Equal(char.ToUpperInvariant(value[i]), upperInvariant[i]);
            }
        }
    }

    [Theory]
    [InlineData('a', true)]
    [InlineData('A', true)]
    [InlineData('G', true)]
    [InlineData('Z', true)]
    [InlineData('z', true)]
    [InlineData('0', false)]
    [InlineData('\0', false)]
    [InlineData(' ', false)]
    [InlineData('ö', false)]
    public static void Test_IsAsciiLetter(char value, bool expected)
    {
        Assert.Equal(expected, value.IsAsciiLetter());
    }

    [Theory]
    [InlineData('0', true)]
    [InlineData('5', true)]
    [InlineData('9', true)]
    [InlineData('-', true)]
    [InlineData('+', true)]
    [InlineData('a', false)]
    [InlineData('A', false)]
    [InlineData('G', false)]
    [InlineData('\0', false)]
    public static void Test_IsAsciiNumeric(char value, bool expected)
    {
        Assert.Equal(expected, value.IsAsciiNumeric());
    }
}
