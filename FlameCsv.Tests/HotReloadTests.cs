using System.Collections;
using System.Reflection;
using System.Reflection.Metadata;
using FlameCsv.Enumeration;
using FlameCsv.Reading.Internal;
using FlameCsv.Tests.Binding;
using FlameCsv.Tests.TestData;
using FlameCsv.Utilities;

namespace FlameCsv.Tests;

public static class HotReloadTests
{
    [Fact]
    public static void Should_Clear_Caches_On_Hot_Reload()
    {
        if (!MetadataUpdater.IsSupported) return;

        // options
        var o = new CsvOptions<char>();
        var c1 = o.GetConverter<int>();
        Assert.NotEmpty(o.ConverterCache);
        Assert.Same(c1, o.GetConverter<int>());

        // typemap
        var dm = ObjTypeMap_Simple.Default.GetDematerializer(CsvOptions<char>.Default);
        Assert.Same(dm, ObjTypeMap_Simple.Default.GetDematerializer(CsvOptions<char>.Default));

        var noheader = new CsvOptions<char> { HasHeader = false };
        var mnh = ObjTypeMap_Simple.Default.GetMaterializer(noheader);
        Assert.Same(mnh, ObjTypeMap_Simple.Default.GetMaterializer(noheader));

        var mh = ObjTypeMap_Simple.Default.GetMaterializer(["a", "b", "c"], CsvOptions<char>.Default);
        Assert.Same(mh, ObjTypeMap_Simple.Default.GetMaterializer(["a", "b", "c"], CsvOptions<char>.Default));

        // record
        using var state = new CsvRecordEnumerator<char>(default, CsvOptions<char>.Default);
        ((IRecordOwner)state).MaterializerCache.Add(new object(), new object());

        // trimming caches
        using var cache = new TrimmingCache<object, object>();
        cache.Add(new object(), new object());

        // writer
        using var writer = CsvWriter.Create(TextWriter.Null);
        writer.WriteRecord(new Obj());
        (ICollection? writeCache, object? writeKey, object? writeValue) = GetWriterCache(writer);
        Assert.NotEmpty(writeCache);
        Assert.NotNull(writeKey);
        Assert.NotNull(writeValue);

        // hot reload
        HotReloadService.ClearCache(null);

        // options
        Assert.Empty(o.ConverterCache);
        Assert.NotSame(c1, o.GetConverter<int>());

        // typemap
        Assert.NotSame(dm, ObjTypeMap_Simple.Default.GetDematerializer(CsvOptions<char>.Default));
        Assert.NotSame(mnh, ObjTypeMap_Simple.Default.GetMaterializer(noheader));
        Assert.NotSame(mh, ObjTypeMap_Simple.Default.GetMaterializer(["a", "b", "c"], CsvOptions<char>.Default));

        // record
        Assert.Empty(((IRecordOwner)state).MaterializerCache);

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
