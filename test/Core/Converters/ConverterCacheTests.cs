using System.Reflection;
using FlameCsv.Attributes;
using FlameCsv.Converters;
using FlameCsv.Converters.Formattable;

namespace FlameCsv.Tests.Converters;

public class ConverterCacheTests
{
    private class Shim
    {
        [CsvConverter<SpanTextConverter<int>>]
        public int Id { get; set; }
    }

    [Fact]
    public static void Should_Cache_Member_Converters()
    {
        var opts = new CsvOptions<char>();

        var d1 = opts.GetConverter<int>();
        var d2 = opts.GetConverter(typeof(int));

        Assert.Same(d1, d2);

        var d3 = opts.Aot.GetConverter<int>();
        Assert.Same(d1, d3);
        Assert.Same(d1, opts.Aot.GetConverter<int>());

        var attr = typeof(Shim).GetProperty("Id")!.GetCustomAttributes<CsvConverterAttribute>().Single();

        Assert.True(attr.TryCreateConverter<char>(typeof(int), opts, out var c1));
        Assert.NotSame(d1, c1);

        Assert.True(attr.TryCreateConverter<char>(typeof(int), opts, out var c2));
        Assert.Same(c1, c2);

        Assert.Equal(2, opts.ConverterCache.Count);

        var n1 = opts.Aot.GetOrCreateNullable(o => o.GetConverter<int>());
        Assert.Same(((NullableConverter<char, int>)n1)._converter, d1);
    }
}
