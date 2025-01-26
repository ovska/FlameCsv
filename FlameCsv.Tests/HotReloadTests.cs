using System.Reflection.Metadata;
using FlameCsv.Tests.Binding;
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
        Assert.NotEmpty(o._converterCache);
        Assert.Same(c1, o.GetConverter<int>());

        // typemap
        var dm = ObjTypeMap_Simple.Instance.GetDematerializer(CsvOptions<char>.Default);
        Assert.Same(dm, ObjTypeMap_Simple.Instance.GetDematerializer(CsvOptions<char>.Default));

        var noheader = new CsvOptions<char> { HasHeader = false };
        var mnh = ObjTypeMap_Simple.Instance.GetMaterializer(noheader);
        Assert.Same(mnh, ObjTypeMap_Simple.Instance.GetMaterializer(noheader));

        var mh = ObjTypeMap_Simple.Instance.GetMaterializer(["a", "b", "c"], CsvOptions<char>.Default);
        Assert.Same(mh, ObjTypeMap_Simple.Instance.GetMaterializer(["a", "b", "c"], CsvOptions<char>.Default));

        // record
        using var state = new EnumeratorState<char>(CsvOptions<char>.Default);
        state.MaterializerCache.Add(new object(), new object());

        // trimming caches
        using var cache = new TrimmingCache<object, object>();
        cache.Add(new object(), new object());

        HotReloadService.ClearCache(null);

        // options
        Assert.Empty(o._converterCache);
        Assert.NotSame(c1, o.GetConverter<int>());

        // typemap
        Assert.NotSame(dm, ObjTypeMap_Simple.Instance.GetDematerializer(CsvOptions<char>.Default));
        Assert.NotSame(mnh, ObjTypeMap_Simple.Instance.GetMaterializer(noheader));
        Assert.NotSame(mh, ObjTypeMap_Simple.Instance.GetMaterializer(["a", "b", "c"], CsvOptions<char>.Default));

        // record
        Assert.Empty(state.MaterializerCache);

        // trimming caches
        Assert.Empty(cache);
    }
}
