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
        Assert.True(Utf8Util.SequenceEqual(default, default, StringComparison.Ordinal));
        Assert.True(Utf8Util.SequenceEqual(default, default, StringComparison.OrdinalIgnoreCase));

        Assert.False(Utf8Util.SequenceEqual(_utf, default, StringComparison.Ordinal));
        Assert.False(Utf8Util.SequenceEqual(_utf, default, StringComparison.OrdinalIgnoreCase));

        Assert.False(Utf8Util.SequenceEqual(default, _str, StringComparison.Ordinal));
        Assert.False(Utf8Util.SequenceEqual(default, _str, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public static void Should_Equals_Ordinal()
    {
        Assert.True(Utf8Util.SequenceEqual(_utf, _str, StringComparison.Ordinal));
        Assert.True(Utf8Util.SequenceEqual(_utf, _str, StringComparison.OrdinalIgnoreCase));

        Assert.False(Utf8Util.SequenceEqual(_utf, _str.ToUpper(), StringComparison.Ordinal));
        Assert.True(Utf8Util.SequenceEqual(_utf, _str.ToUpper(), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public static void Should_Equals_Weird_Characters()
    {
        Assert.True(Utf8Util.SequenceEqual("κόσμε"u8, "κόσμε", StringComparison.Ordinal));
        Assert.True(Utf8Util.SequenceEqual("κόσμε"u8, "κόσμε", StringComparison.OrdinalIgnoreCase));

        Assert.False(Utf8Util.SequenceEqual("κόσμε"u8, "κόσμε".ToUpper(), StringComparison.Ordinal));
        Assert.True(Utf8Util.SequenceEqual("κόσμε"u8, "κόσμε".ToUpper(), StringComparison.OrdinalIgnoreCase));

        Assert.True(Utf8Util.SequenceEqual(WeirdnessU8, Weirdness, StringComparison.Ordinal));
        Assert.True(Utf8Util.SequenceEqual(WeirdnessU8, Weirdness, StringComparison.OrdinalIgnoreCase));

        Assert.False(Utf8Util.SequenceEqual(WeirdnessU8, Weirdness.ToUpper(), StringComparison.Ordinal));
        Assert.True(Utf8Util.SequenceEqual(WeirdnessU8, Weirdness.ToUpper(), StringComparison.OrdinalIgnoreCase));
    }


}
