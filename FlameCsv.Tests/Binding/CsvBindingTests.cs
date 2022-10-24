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

    [Fact]
    public static void Should_Not_Support_Interfaces()
    {
        Assert.Throws<NotSupportedException>(() => CsvBinding.For<IFace>(0, x => x.Prop));
    }

    [Fact]
    public static void Should_Check_Applicability()
    {
        var idBinding = new CsvBinding(0, typeof(Base).GetProperty("Id")!);
        var nameBinding = new CsvBinding(0, typeof(Class).GetProperty("Name")!);
        Assert.True(idBinding.IsApplicableTo<Base>());
        Assert.True(idBinding.IsApplicableTo<Class>());
        Assert.False(nameBinding.IsApplicableTo<Base>());
        Assert.True(nameBinding.IsApplicableTo<Class>());

        var structBinding = new CsvBinding(0, typeof(Struct).GetProperty("Prop")!);
        Assert.True(structBinding.IsApplicableTo<Struct>());
        Assert.False(structBinding.IsApplicableTo<IFace>());
    }

    [Fact]
    public static void Should_Handle_Ignored()
    {
        var ignored = CsvBinding.Ignore(2);
        Assert.Equal(ignored, CsvBinding.Ignore(2));
        Assert.Equal(ignored.GetHashCode(), CsvBinding.Ignore(2).GetHashCode());
    }

    [Fact]
    public static void Should_Implement_IEquatable()
    {
        var propInfo = new CsvBinding(0, typeof(Base).GetProperty("Id")!);
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
        Assert.Throws<ArgumentNullException>(() => new CsvBindingCollection<Class>(null!));
        Assert.Throws<CsvBindingException>(() => new CsvBindingCollection<Class>(Enumerable.Empty<CsvBinding>()));

        Assert.Throws<CsvBindingException>(
            () => new CsvBindingCollection<Class>(new[] { CsvBinding.Ignore(0), CsvBinding.Ignore(1) }));

        Assert.Throws<CsvBindingException>(
            () => new CsvBindingCollection<Class>(
                new[]
                {
                    CsvBinding.For<Class>(0, x => x.Id),
                    CsvBinding.For<Class>(0, x => x.Name),
                }));

        Assert.Throws<CsvBindingException>(
            () => new CsvBindingCollection<Class>(
                new[]
                {
                    CsvBinding.For<Class>(0, x => x.Id),
                    CsvBinding.For<Class>(1, x => x.Id),
                }));

        Assert.Throws<CsvBindingException>(
            () => new CsvBindingCollection<Class>(
                new[]
                {
                    CsvBinding.For<Struct>(0, x => x.Prop),
                }));
    }
}
