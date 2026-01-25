using System.Collections;
using System.Reflection;
using System.Reflection.Metadata;
using FlameCsv.Attributes;
using FlameCsv.Enumeration;
using FlameCsv.IO.Internal;
using FlameCsv.Tests.TestData;
using FlameCsv.Utilities;

namespace FlameCsv.Tests;

public static partial class HotReloadTests
{
    [CsvTypeMap<char, Obj>]
    private partial class TestTypeMap;

    [Fact]
    public static void Should_Clear_Caches_On_Hot_Reload()
    {
        Assert.SkipUnless(MetadataUpdater.IsSupported, "Hot reload is not enabled.");

        // options
        var o = new CsvOptions<char>();
        var c1 = o.GetConverter<int>();
        Assert.NotEmpty(o.ConverterCache);
        Assert.Same(c1, o.GetConverter<int>());

        // typemap
        var dm = TestTypeMap.Default.GetDematerializer(CsvOptions<char>.Default);
        Assert.Same(dm, TestTypeMap.Default.GetDematerializer(CsvOptions<char>.Default));

        var noheader = new CsvOptions<char> { HasHeader = false };
        var mnh = TestTypeMap.Default.GetMaterializer(noheader);
        Assert.Same(mnh, TestTypeMap.Default.GetMaterializer(noheader));

        var mh = TestTypeMap.Default.GetMaterializer(["a", "b", "c"], CsvOptions<char>.Default);
        Assert.Same(mh, TestTypeMap.Default.GetMaterializer(["a", "b", "c"], CsvOptions<char>.Default));

        // record
        using var state = new CsvRecordEnumerator<char>(CsvOptions<char>.Default, EmptyBufferReader<char>.Instance);
        state.MaterializerCache.Add(new object(), new object());

        // trimming caches
        using var cache = new TrimmingCache<object, object>();
        cache.Add(new object(), new object());

        // writer
        using var writer = Csv.To(TextWriter.Null).ToWriter();
        writer.WriteRecord(new Obj());
        (ICollection writeCache, object? writeKey, object? writeValue) = GetWriterCache(writer);
        Assert.NotEmpty(writeCache);
        Assert.NotNull(writeKey);
        Assert.NotNull(writeValue);

        // hot reload
        HotReloadService.ClearCache(null);

        // options
        Assert.Empty(o.ConverterCache);
        Assert.NotSame(c1, o.GetConverter<int>());

        // typemap
        Assert.NotSame(dm, TestTypeMap.Default.GetDematerializer(CsvOptions<char>.Default));
        Assert.NotSame(mnh, TestTypeMap.Default.GetMaterializer(noheader));
        Assert.NotSame(mh, TestTypeMap.Default.GetMaterializer(["a", "b", "c"], CsvOptions<char>.Default));

        // record
        Assert.Empty(state.MaterializerCache);

        // trimming caches
        Assert.Empty(cache);

        // writer
        (writeCache, writeKey, writeValue) = GetWriterCache(writer);
        Assert.Empty(writeCache);
        Assert.Null(writeKey);
        Assert.Null(writeValue);
    }

    private static (ICollection, object?, object?) GetWriterCache(CsvWriter<char> writer)
    {
        var type = typeof(CsvWriter<char>);

        return (
            (ICollection)type.GetField("_dematerializerCache", BindingFlags.NonPublic)!.GetValue(writer)!,
            type.GetField("_previousKey", BindingFlags.NonPublic)!.GetValue(writer),
            type.GetField("_previousValue", BindingFlags.NonPublic)!.GetValue(writer)
        );
    }
}
