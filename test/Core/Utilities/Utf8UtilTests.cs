using System.Text;
using FlameCsv.Utilities;

namespace FlameCsv.Tests.Utilities;

public static class Utf8UtilTests
{
    private static byte[] ToUTF8(string input) => Encoding.UTF8.GetBytes(input);

    // ReSharper disable once ConvertToConstant.Local
    private static readonly string _strAbc = "abcdefghijklmnopqrstuvwxyzoäö^üñ";
    private static readonly byte[] _byteAbc = ToUTF8(_strAbc);

    private static ReadOnlySpan<byte> WeirdnessU8 =>
        "Ḽơᶉëᶆ ȋṕšᶙṁ ḍỡḽǭᵳ ʂǐť ӓṁệẗ, ĉṓɲṩḙċťᶒțûɾ ấɖḯƥĭṩčįɳġ ḝłįʈ, șếᶑ ᶁⱺ ẽḭŭŝḿꝋď ṫĕᶆᶈṓɍ ỉñḉīḑȋᵭṵńť ṷŧ ḹẩḇőꝛế éȶ đꝍꞎôꝛȇ ᵯáꞡᶇā ąⱡîɋṹẵ. Юникод!"u8;
    private const string Weirdness =
        "Ḽơᶉëᶆ ȋṕšᶙṁ ḍỡḽǭᵳ ʂǐť ӓṁệẗ, ĉṓɲṩḙċťᶒțûɾ ấɖḯƥĭṩčįɳġ ḝłįʈ, șếᶑ ᶁⱺ ẽḭŭŝḿꝋď ṫĕᶆᶈṓɍ ỉñḉīḑȋᵭṵńť ṷŧ ḹẩḇőꝛế éȶ đꝍꞎôꝛȇ ᵯáꞡᶇā ąⱡîɋṹẵ. Юникод!";

    [Fact]
    public static void Should_Equals_Empty()
    {
        Assert.True(Utf8Util.SequenceEqual(Span<byte>.Empty, Span<char>.Empty, StringComparison.Ordinal));
        Assert.True(Utf8Util.SequenceEqual(Span<byte>.Empty, Span<char>.Empty, StringComparison.OrdinalIgnoreCase));

        Assert.True(Utf8Util.SequenceEqual(Span<byte>.Empty, Span<byte>.Empty, StringComparison.Ordinal));
        Assert.True(Utf8Util.SequenceEqual(Span<byte>.Empty, Span<byte>.Empty, StringComparison.OrdinalIgnoreCase));

        Assert.False(Utf8Util.SequenceEqual(_byteAbc, Span<byte>.Empty, StringComparison.Ordinal));
        Assert.False(Utf8Util.SequenceEqual(_byteAbc, Span<byte>.Empty, StringComparison.OrdinalIgnoreCase));

        Assert.False(Utf8Util.SequenceEqual(_byteAbc, Span<char>.Empty, StringComparison.Ordinal));
        Assert.False(Utf8Util.SequenceEqual(_byteAbc, Span<char>.Empty, StringComparison.OrdinalIgnoreCase));

        Assert.False(Utf8Util.SequenceEqual(Span<byte>.Empty, _strAbc, StringComparison.Ordinal));
        Assert.False(Utf8Util.SequenceEqual(Span<byte>.Empty, _strAbc, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public static void Should_Equals_Ordinal()
    {
        Assert.True(Utf8Util.SequenceEqual(_byteAbc, _strAbc, StringComparison.Ordinal));
        Assert.True(Utf8Util.SequenceEqual(_byteAbc, _strAbc, StringComparison.OrdinalIgnoreCase));

        Assert.True(Utf8Util.SequenceEqual(_byteAbc.AsSpan(..^2), _strAbc.AsSpan(..^1), StringComparison.Ordinal));
        Assert.True(
            Utf8Util.SequenceEqual(_byteAbc.AsSpan(..^2), _strAbc.AsSpan(..^1), StringComparison.OrdinalIgnoreCase)
        );

        Assert.False(Utf8Util.SequenceEqual(_byteAbc, _strAbc.ToUpper(), StringComparison.Ordinal));
        Assert.True(Utf8Util.SequenceEqual(_byteAbc, _strAbc.ToUpper(), StringComparison.OrdinalIgnoreCase));

        Assert.False(Utf8Util.SequenceEqual(_byteAbc, ToUTF8(_strAbc.ToUpper()), StringComparison.Ordinal));
        Assert.True(Utf8Util.SequenceEqual(_byteAbc, ToUTF8(_strAbc.ToUpper()), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public static void Should_Equals_Weird_Characters()
    {
        Assert.True(Utf8Util.SequenceEqual("κόσμε"u8, "κόσμε", StringComparison.Ordinal));
        Assert.True(Utf8Util.SequenceEqual("κόσμε"u8, "κόσμε", StringComparison.OrdinalIgnoreCase));

        Assert.True(Utf8Util.SequenceEqual("κόσμε"u8, "κόσμε"u8, StringComparison.Ordinal));
        Assert.True(Utf8Util.SequenceEqual("κόσμε"u8, "κόσμε"u8, StringComparison.OrdinalIgnoreCase));

        Assert.False(Utf8Util.SequenceEqual("κόσμε"u8, "κόσμε".ToUpper(), StringComparison.Ordinal));
        Assert.True(Utf8Util.SequenceEqual("κόσμε"u8, "κόσμε".ToUpper(), StringComparison.OrdinalIgnoreCase));

        Assert.False(Utf8Util.SequenceEqual("κόσμε"u8, ToUTF8("κόσμε".ToUpper()), StringComparison.Ordinal));
        Assert.True(Utf8Util.SequenceEqual("κόσμε"u8, ToUTF8("κόσμε".ToUpper()), StringComparison.OrdinalIgnoreCase));

        Assert.True(Utf8Util.SequenceEqual(WeirdnessU8, Weirdness, StringComparison.Ordinal));
        Assert.True(Utf8Util.SequenceEqual(WeirdnessU8, Weirdness, StringComparison.OrdinalIgnoreCase));

        Assert.True(Utf8Util.SequenceEqual(WeirdnessU8, ToUTF8(Weirdness), StringComparison.Ordinal));
        Assert.True(Utf8Util.SequenceEqual(WeirdnessU8, ToUTF8(Weirdness), StringComparison.OrdinalIgnoreCase));

        Assert.False(Utf8Util.SequenceEqual(WeirdnessU8, Weirdness.ToUpper(), StringComparison.Ordinal));
        Assert.True(Utf8Util.SequenceEqual(WeirdnessU8, Weirdness.ToUpper(), StringComparison.OrdinalIgnoreCase));

        Assert.False(Utf8Util.SequenceEqual(WeirdnessU8, ToUTF8(Weirdness.ToUpper()), StringComparison.Ordinal));
        Assert.True(
            Utf8Util.SequenceEqual(WeirdnessU8, ToUTF8(Weirdness.ToUpper()), StringComparison.OrdinalIgnoreCase)
        );
    }
}
