using System.Text;
using FlameCsv.Extensions;

namespace FlameCsv.Tests.Extensions;

public static class Utf8UtilTests
{
    private const string _str = "abcdefghijklmnopqrstuvwxyzoäö^üñ";
    private static readonly byte[] _utf = Encoding.UTF8.GetBytes(_str);

    private static ReadOnlySpan<byte> WeirdnessU8
        => "Ḽơᶉëᶆ ȋṕšᶙṁ ḍỡḽǭᵳ ʂǐť ӓṁệẗ, ĉṓɲṩḙċťᶒțûɾ ấɖḯƥĭṩčįɳġ ḝłįʈ, șếᶑ ᶁⱺ ẽḭŭŝḿꝋď ṫĕᶆᶈṓɍ ỉñḉīḑȋᵭṵńť ṷŧ ḹẩḇőꝛế éȶ đꝍꞎôꝛȇ ᵯáꞡᶇā ąⱡîɋṹẵ. Юникод!"u8;
    private const string Weirdness
        = "Ḽơᶉëᶆ ȋṕšᶙṁ ḍỡḽǭᵳ ʂǐť ӓṁệẗ, ĉṓɲṩḙċťᶒțûɾ ấɖḯƥĭṩčįɳġ ḝłįʈ, șếᶑ ᶁⱺ ẽḭŭŝḿꝋď ṫĕᶆᶈṓɍ ỉñḉīḑȋᵭṵńť ṷŧ ḹẩḇőꝛế éȶ đꝍꞎôꝛȇ ᵯáꞡᶇā ąⱡîɋṹẵ. Юникод!";

    [Fact]
    public static void Should_Equals_Empty()
    {
        Assert.True(Utf8Util.SequenceEqual(Span<byte>.Empty, Span<char>.Empty, StringComparison.Ordinal));
        Assert.True(Utf8Util.SequenceEqual(Span<byte>.Empty, Span<char>.Empty, StringComparison.OrdinalIgnoreCase));

        Assert.True(Utf8Util.SequenceEqual(Span<byte>.Empty, Span<byte>.Empty, StringComparison.Ordinal));
        Assert.True(Utf8Util.SequenceEqual(Span<byte>.Empty, Span<byte>.Empty, StringComparison.OrdinalIgnoreCase));

        Assert.False(Utf8Util.SequenceEqual(_utf, Span<byte>.Empty, StringComparison.Ordinal));
        Assert.False(Utf8Util.SequenceEqual(_utf, Span<byte>.Empty, StringComparison.OrdinalIgnoreCase));

        Assert.False(Utf8Util.SequenceEqual(_utf, Span<char>.Empty, StringComparison.Ordinal));
        Assert.False(Utf8Util.SequenceEqual(_utf, Span<char>.Empty, StringComparison.OrdinalIgnoreCase));

        Assert.False(Utf8Util.SequenceEqual(Span<byte>.Empty, _str, StringComparison.Ordinal));
        Assert.False(Utf8Util.SequenceEqual(Span<byte>.Empty, _str, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public static void Should_Equals_Ordinal()
    {
        Assert.True(Utf8Util.SequenceEqual(_utf, _str, StringComparison.Ordinal));
        Assert.True(Utf8Util.SequenceEqual(_utf, _str, StringComparison.OrdinalIgnoreCase));

        Assert.True(Utf8Util.SequenceEqual(_utf.AsSpan(..^2), _str.AsSpan(..^1), StringComparison.Ordinal));
        Assert.True(Utf8Util.SequenceEqual(_utf.AsSpan(..^2), _str.AsSpan(..^1), StringComparison.OrdinalIgnoreCase));

        Assert.False(Utf8Util.SequenceEqual(_utf, _str.ToUpper(), StringComparison.Ordinal));
        Assert.True(Utf8Util.SequenceEqual(_utf, _str.ToUpper(), StringComparison.OrdinalIgnoreCase));

        Assert.False(Utf8Util.SequenceEqual(_utf, Encoding.UTF8.GetBytes(_str.ToUpper()), StringComparison.Ordinal));
        Assert.True(Utf8Util.SequenceEqual(_utf, Encoding.UTF8.GetBytes(_str.ToUpper()), StringComparison.OrdinalIgnoreCase));
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

        Assert.False(Utf8Util.SequenceEqual("κόσμε"u8, Encoding.UTF8.GetBytes("κόσμε".ToUpper()), StringComparison.Ordinal));
        Assert.True(Utf8Util.SequenceEqual("κόσμε"u8, Encoding.UTF8.GetBytes("κόσμε".ToUpper()), StringComparison.OrdinalIgnoreCase));

        Assert.True(Utf8Util.SequenceEqual(WeirdnessU8, Weirdness, StringComparison.Ordinal));
        Assert.True(Utf8Util.SequenceEqual(WeirdnessU8, Weirdness, StringComparison.OrdinalIgnoreCase));

        Assert.True(Utf8Util.SequenceEqual(WeirdnessU8, Encoding.UTF8.GetBytes(Weirdness), StringComparison.Ordinal));
        Assert.True(Utf8Util.SequenceEqual(WeirdnessU8, Encoding.UTF8.GetBytes(Weirdness), StringComparison.OrdinalIgnoreCase));

        Assert.False(Utf8Util.SequenceEqual(WeirdnessU8, Weirdness.ToUpper(), StringComparison.Ordinal));
        Assert.True(Utf8Util.SequenceEqual(WeirdnessU8, Weirdness.ToUpper(), StringComparison.OrdinalIgnoreCase));

        Assert.False(Utf8Util.SequenceEqual(WeirdnessU8, Encoding.UTF8.GetBytes(Weirdness.ToUpper()), StringComparison.Ordinal));
        Assert.True(Utf8Util.SequenceEqual(WeirdnessU8, Encoding.UTF8.GetBytes(Weirdness.ToUpper()), StringComparison.OrdinalIgnoreCase));
    }


}
