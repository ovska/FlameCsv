using System.Reflection;
using FlameCsv.Binding;

namespace FlameCsv.Tests.Binding;

public static class DefaultHeaderMatcherTests
{
    private class Shim
    {
        // ReSharper disable once UnassignedGetOnlyAutoProperty
        // ReSharper disable once UnusedMember.Local
        public int Prop { get; set; }
    }

    private static HeaderBindingArgs GetArgs(int index, MemberInfo? member)
        => new(index, member!.Name, member, 0);

    [Fact]
    public static void Should_Match_Text_To_Name_Case_Sensitive()
    {
        var matcher = new DefaultTextHeaderMatcher(StringComparison.Ordinal);
        var member = typeof(Shim).GetProperty("Prop")!;
        Assert.Equal(CsvBinding.ForMember<Shim>(0, member), matcher.TryMatch<Shim>("Prop", GetArgs(0, member)));
        Assert.Null(matcher.TryMatch<Shim>("prop", GetArgs(1, member)));
    }

    [Fact]
    public static void Should_Match_Text_To_Name_Case_Inensitive()
    {
        var matcher = new DefaultTextHeaderMatcher(StringComparison.OrdinalIgnoreCase);
        var member = typeof(Shim).GetProperty("Prop")!;
        Assert.Equal(CsvBinding.ForMember<Shim>(0, member), matcher.TryMatch<Shim>("Prop", GetArgs(0, member)));
        Assert.Equal(CsvBinding.ForMember<Shim>(1, member), matcher.TryMatch<Shim>("prop", GetArgs(1, member)));
    }

    [Fact]
    public static void Should_Match_Bytes_To_Name_Case_Sensitive()
    {
        var matcher = new DefaultUtf8HeaderMatcher(StringComparison.Ordinal);
        var member = typeof(Shim).GetProperty("Prop")!;
        Assert.Equal(CsvBinding.ForMember<Shim>(0, member), matcher.TryMatch<Shim>("Prop"u8.ToArray(), GetArgs(0, member)));
        Assert.Null(matcher.TryMatch<Shim>("prop"u8.ToArray(), GetArgs(1, member)));
    }

    [Fact]
    public static void Should_Match_Bytes_To_Name_Case_Inensitive()
    {
        var matcher = new DefaultUtf8HeaderMatcher(StringComparison.OrdinalIgnoreCase);
        var member = typeof(Shim).GetProperty("Prop")!;
        Assert.Equal(CsvBinding.ForMember<Shim>(0, member), matcher.TryMatch<Shim>("Prop"u8.ToArray(), GetArgs(0, member)));
        Assert.Equal(CsvBinding.ForMember<Shim>(1, member), matcher.TryMatch<Shim>("prop"u8.ToArray(), GetArgs(1, member)));
    }

    [Fact]
    public static void Should_Validate_Parameter()
    {
        Assert.ThrowsAny<ArgumentException>(() => new DefaultTextHeaderMatcher((StringComparison)int.MaxValue));
        Assert.ThrowsAny<ArgumentException>(() => new DefaultUtf8HeaderMatcher((StringComparison)int.MaxValue));
    }
}
