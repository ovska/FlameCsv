using FlameCsv.Binding;
using FlameCsv.Exceptions;

// ReSharper disable UnusedAutoPropertyAccessor.Local

namespace FlameCsv.Tests.Binding;

public static class CsvBindingTests
{
    private abstract class Base
    {
        public int Id { get; set; }
    }

    private class Class : Base
    {
        public string? Name { get; set; }
    }

    private interface IFace
    {
        public int Prop { get; set; }
    }

    private struct Struct : IFace
    {
        public int Prop { get; set; }
    }

    [Fact(Skip = "Investigate further")]
    public static void Should_Not_Support_Interfaces()
    {
        Assert.Throws<NotSupportedException>(() => CsvBinding.For<IFace>(0, x => x.Prop));
    }

    [Fact]
    public static void Should_Handle_Ignored()
    {
        var ignored = CsvBinding.Ignore<Class>(2);
        Assert.Equal(ignored, CsvBinding.Ignore<Class>(2));
        Assert.Equal(ignored.GetHashCode(), CsvBinding.Ignore<Class>(2).GetHashCode());
    }

    [Fact]
    public static void Should_Implement_IEquatable()
    {
        var propInfo = CsvBinding.ForMember<Base>(0, typeof(Base).GetProperty("Id")!);
        var expr = CsvBinding.For<Base>(0, b => b.Id);
        Assert.Equal(propInfo, expr);
        Assert.True(propInfo.Equals((object)expr));
        Assert.True(propInfo == expr);
        Assert.Equal(propInfo.GetHashCode(), expr.GetHashCode());
        Assert.False(propInfo != expr);

        Assert.False(expr.Equals(CsvBinding.For<Base>(1, b => b.Id)));
        Assert.True(expr.Equals(CsvBinding.For<Class>(0, b => b.Id)));
        Assert.False(expr.Equals(CsvBinding.For<Class>(0, b => b.Name)));
    }

    [Fact]
    public static void Should_Validate_Collection()
    {
        Assert.Throws<ArgumentNullException>(
            () => new CsvBindingCollection<Class>(null!, false));
        Assert.Throws<ArgumentException>(
            () => new CsvBindingCollection<Class>(Enumerable.Empty<CsvBinding<Class>>(), false));

        Assert.ThrowsAny<CsvBindingException>(
            () => new CsvBindingCollection<Class>(
                new[] { CsvBinding.Ignore<Class>(0), CsvBinding.Ignore<Class>(1) },
                false));

        Assert.ThrowsAny<CsvBindingException>(
            () => new CsvBindingCollection<Class>(
                new[]
                {
                    CsvBinding.For<Class>(0, x => x.Id),
                    CsvBinding.For<Class>(0, x => x.Name),
                },
                false));

        Assert.ThrowsAny<CsvBindingException>(
            () => new CsvBindingCollection<Class>(
                new[]
                {
                    CsvBinding.For<Class>(0, x => x.Id),
                    CsvBinding.For<Class>(1, x => x.Id),
                },
                false));

        Assert.ThrowsAny<CsvBindingException>(
            () => new CsvBindingCollection<Struct>(
                new[]
                {
                    CsvBinding.ForMember<Struct>(0, typeof(Class).GetProperties()[0]),
                },
                false));
    }
}
