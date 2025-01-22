using System.Reflection.Metadata;
using FlameCsv.Binding;
using FlameCsv.Converters;
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
        var c2 = o.GetOrCreate(cacheKey: Type.Missing, static _ => new StringTextConverter());
        Assert.Same(c2, o.GetOrCreate(cacheKey: Type.Missing, static _ => new StringTextConverter()));
        Assert.NotEmpty(o._explicitCache);

        // typemap
        var dm = CsvTypeMap.GetDematerializer(ObjTypeMap_Simple.Instance, CsvOptions<char>.Default);
        Assert.Same(dm, CsvTypeMap.GetDematerializer(ObjTypeMap_Simple.Instance, CsvOptions<char>.Default));

        // record
        using var state = new EnumeratorState<char>(CsvOptions<char>.Default);
        state.MaterializerCache.Add(new object(), new object());

        // trimming caches
        using var cache = new TrimmingCache<object, object>();
        cache.Add(new object(), new object());

        HotReloadService.ClearCache(null);

        // options
        Assert.Empty(o._converterCache);
        Assert.Empty(o._explicitCache);
        Assert.NotSame(c1, o.GetConverter<int>());
        Assert.NotSame(c2, o.GetOrCreate(cacheKey: Type.Missing, static _ => new StringTextConverter()));

        // typemap
        Assert.NotSame(dm, CsvTypeMap.GetDematerializer(ObjTypeMap_Simple.Instance, CsvOptions<char>.Default));

        // record
        Assert.Empty(state.MaterializerCache);

        // trimming caches
        Assert.Empty(cache);
    }
}
