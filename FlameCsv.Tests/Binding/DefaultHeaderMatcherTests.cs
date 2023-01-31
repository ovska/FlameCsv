using System.Reflection;
using System.Text;
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
        => new()
        {
            Index = index,
            Value = member!.Name,
            Member = member,
            Order = 0,
            TargetType = typeof(Shim),
        };

    [Fact]
    public static void Should_Match_Text_To_Name_Case_Sensitive()
    {
        var fn = HeaderMatcherDefaults.MatchText(StringComparison.Ordinal);
        var member = typeof(Shim).GetProperty("Prop")!;
        Assert.Equal(new CsvBinding(0, member), fn("Prop", GetArgs(0, member)));
        Assert.Null(fn("prop", GetArgs(1, member)));
    }

    [Fact]
    public static void Should_Match_Text_To_Name_Case_Inensitive()
    {
        var fn = HeaderMatcherDefaults.MatchText(StringComparison.OrdinalIgnoreCase);
        var member = typeof(Shim).GetProperty("Prop")!;
        Assert.Equal(new CsvBinding(0, member), fn("Prop", GetArgs(0, member)));
        Assert.Equal(new CsvBinding(1, member), fn("prop", GetArgs(1, member)));
    }

    [Fact]
    public static void Should_Match_Bytes_To_Name_Case_Sensitive()
    {
        var fn = HeaderMatcherDefaults.MatchUtf8(StringComparison.Ordinal);
        var member = typeof(Shim).GetProperty("Prop")!;
        Assert.Equal(new CsvBinding(0, member), fn("Prop"u8.ToArray(), GetArgs(0, member)));
        Assert.Null(fn("prop"u8.ToArray(), GetArgs(1, member)));
    }

    [Fact]
    public static void Should_Match_Bytes_To_Name_Case_Inensitive()
    {
        var fn = HeaderMatcherDefaults.MatchUtf8(StringComparison.OrdinalIgnoreCase);
        var member = typeof(Shim).GetProperty("Prop")!;
        Assert.Equal(new CsvBinding(0, member), fn("Prop"u8.ToArray(), GetArgs(0, member)));
        Assert.Equal(new CsvBinding(1, member), fn("prop"u8.ToArray(), GetArgs(1, member)));
    }

    [Fact]
    public static void Should_Validate_Parameter()
    {
        Assert.ThrowsAny<ArgumentException>(() => HeaderMatcherDefaults.MatchText((StringComparison)int.MaxValue));
        Assert.ThrowsAny<ArgumentException>(() => HeaderMatcherDefaults.MatchUtf8((StringComparison)int.MaxValue));
    }
}
