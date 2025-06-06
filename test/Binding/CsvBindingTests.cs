using System.Buffers;
using System.Text;
using FlameCsv.Attributes;
using FlameCsv.Binding;
using FlameCsv.Exceptions;
using FlameCsv.IO.Internal;
using FlameCsv.Writing;

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

    private class CacheTest
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    private record RecordTest(int Id, string Name);

    [Fact]
    public static void Should_Bind_To_Record()
    {
        // records have parameters and init properties, ensure the binder only binds to the parameters

        var binder = new CsvReflectionBinder<char>(CsvOptions<char>.Default, ignoreUnmatched: false);
        var materializer = binder.GetMaterializer<RecordTest>(["Id", "Name"]);

        var record = new ConstantRecord("1", "Test");
        Assert.Equal(new RecordTest(1, "Test"), materializer.Parse(ref record));

        var dematerializer = binder.GetDematerializer<RecordTest>();

        var sb = new StringBuilder();
        using (
            var csvWriter = new CsvFieldWriter<char>(
                new StringBuilderBufferWriter(sb, MemoryPool<char>.Shared),
                CsvOptions<char>.Default
            )
        )
        {
            dematerializer.Write(in csvWriter, new(1, "Test"));
        }

        Assert.Equal("1,Test", sb.ToString());
    }

    [Fact]
    public static void Should_Cache()
    {
        var binder = new CsvReflectionBinder<char>(CsvOptions<char>.Default, ignoreUnmatched: false);

        var m1 = binder.GetMaterializer<CacheTest>(["Id", "Name"]);
        var m2 = binder.GetMaterializer<CacheTest>(["Id", "Name"]);
        Assert.Same(m1, m2);
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
        Assert.Equal(propInfo.GetHashCode(), expr.GetHashCode());

        Assert.False(expr.Equals(CsvBinding.For<Base>(1, b => b.Id)));
        Assert.True(expr.Equals(CsvBinding.For<Class>(0, b => b.Id)));
        Assert.False(expr.Equals(CsvBinding.For<Class>(0, b => b.Name)));
    }

    [Fact]
    public static void Should_Validate_Collection()
    {
        Assert.Throws<ArgumentNullException>(() => new CsvBindingCollection<Class>(null!, false));
        Assert.Throws<ArgumentException>(() => new CsvBindingCollection<Class>([], false));

        Assert.ThrowsAny<CsvBindingException>(() =>
            new CsvBindingCollection<Class>([CsvBinding.Ignore<Class>(0), CsvBinding.Ignore<Class>(1)], false)
        );

        Assert.ThrowsAny<CsvBindingException>(() =>
            new CsvBindingCollection<Class>(
                [CsvBinding.For<Class>(0, x => x.Id), CsvBinding.For<Class>(0, x => x.Name)],
                false
            )
        );

        Assert.ThrowsAny<CsvBindingException>(() =>
            new CsvBindingCollection<Class>(
                [CsvBinding.For<Class>(0, x => x.Id), CsvBinding.For<Class>(1, x => x.Id)],
                false
            )
        );

        Assert.ThrowsAny<CsvBindingException>(() =>
            new CsvBindingCollection<Struct>([CsvBinding.ForMember<Struct>(0, typeof(Class).GetProperties()[0])], false)
        );
    }

    [Fact]
    public static void Should_Validate()
    {
        Assert.Throws<CsvBindingException>(() => Binder.GetMaterializer<IgnoredAndRequired>(["value"]));
        Assert.Throws<CsvBindingException>(() => Binder.GetMaterializer<MultipleNames>(["value"]));
        Assert.Throws<CsvBindingException>(() => Binder.GetMaterializer<MultipleAliases>(["value"]));
        Assert.Throws<CsvBindingException>(() => Binder.GetMaterializer<MultipleOrders>(["value"]));
        Assert.Throws<CsvBindingException>(() => Binder.GetMaterializer<MultipleIndexes>(["value"]));
    }

    [CsvRequired(MemberName = nameof(Value))]
    private sealed class IgnoredAndRequired
    {
        [CsvIgnore]
        public int Value { get; set; }
    }

    private sealed class MultipleNames
    {
        [CsvHeader("value1")]
        [CsvHeader("value2")]
        public int Value { get; set; }
    }

    private sealed class MultipleAliases
    {
        [CsvHeader("value", Aliases = ["alias1"])]
        [CsvHeader("value", Aliases = ["alias2"])]
        public int Value { get; set; }
    }

    private sealed class MultipleOrders
    {
        [CsvOrder(1)]
        [CsvOrder(2)]
        public int Value { get; set; }
    }

    [CsvIndex(2, MemberName = nameof(Value))]
    private sealed class MultipleIndexes
    {
        [CsvIndex(1)]
        public int Value { get; set; }
    }

    [CsvHeader("should_target_value")]
    private sealed class NoTarget
    {
        public int Value { get; set; }
    }

    private static CsvReflectionBinder<char> Binder => new(CsvOptions<char>.Default, false);
}
