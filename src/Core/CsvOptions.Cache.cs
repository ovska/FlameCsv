using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Utilities;
using FlameCsv.Writing;
using JetBrains.Annotations;

namespace FlameCsv;

public partial class CsvOptions<T>
{
    // trimmingcache doesn't need to be disposed, disposal only clears it from the hot-reload weaktable
    private TrimmingCache<MaterializerKey, object>? _bindingCache;

    [MemberNotNull(nameof(_bindingCache))]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private TrimmingCache<MaterializerKey, object> InitializeBindingCache()
    {
        if (FlameCsvGlobalOptions.CachingDisabled)
            Throw.Unreachable("Caching is disabled.");

        TrimmingCache<MaterializerKey, object> instance = [];

        if (Interlocked.CompareExchange(ref _bindingCache, instance, null) is not null)
        {
            // Another thread already initialized the cache.
            // Dispose of the new instance.
            ((IDisposable)instance).Dispose();
        }

        return _bindingCache;
    }

    internal IMaterializer<T, TValue> GetMaterializer<TValue>(
        ImmutableArray<string> headers,
        bool ignoreUnmatched,
        [RequireStaticDelegate] Func<CsvOptions<T>, ImmutableArray<string>, bool, IMaterializer<T, TValue>> valueFactory
    )
    {
        MakeReadOnly();

        if (FlameCsvGlobalOptions.CachingDisabled)
        {
            return valueFactory(this, headers, ignoreUnmatched);
        }

        TrimmingCache<MaterializerKey, object> cache = _bindingCache ?? InitializeBindingCache();
        MaterializerKey key = new(Comparer, typeof(TValue), ignoreUnmatched, headers);

        if (!cache.TryGetValue(key, out object? materializer))
        {
            cache.Add(key, materializer = valueFactory(this, headers, ignoreUnmatched));
        }

        return (IMaterializer<T, TValue>)materializer;
    }

    internal IMaterializer<T, TValue> GetMaterializer<TValue>(
        CsvTypeMap<T, TValue> typeMap,
        ImmutableArray<string> headers,
        [RequireStaticDelegate]
            Func<CsvOptions<T>, CsvTypeMap<T, TValue>, ImmutableArray<string>, IMaterializer<T, TValue>> valueFactory
    )
    {
        MakeReadOnly();

        if (FlameCsvGlobalOptions.CachingDisabled)
        {
            return valueFactory(this, typeMap, headers);
        }

        TrimmingCache<MaterializerKey, object> cache = _bindingCache ?? InitializeBindingCache();
        MaterializerKey key = new(Comparer, typeMap, headers);

        if (!cache.TryGetValue(key, out object? materializer))
        {
            cache.Add(key, materializer = valueFactory(this, typeMap, headers));
        }

        return (IMaterializer<T, TValue>)materializer;
    }

    internal IDematerializer<T, TValue> GetDematerializer<TValue>(
        [RequireStaticDelegate] Func<CsvOptions<T>, IDematerializer<T, TValue>> valueFactory
    )
    {
        MakeReadOnly();

        if (FlameCsvGlobalOptions.CachingDisabled)
        {
            return valueFactory(this);
        }

        TrimmingCache<MaterializerKey, object> cache = _bindingCache ?? InitializeBindingCache();
        MaterializerKey key = new(typeof(TValue));

        if (!cache.TryGetValue(key, out object? dematerializer))
        {
            cache.Add(key, dematerializer = valueFactory(this));
        }

        return (IDematerializer<T, TValue>)dematerializer;
    }

    internal IDematerializer<T, TValue> GetDematerializer<TValue>(
        CsvTypeMap<T, TValue> typeMap,
        [RequireStaticDelegate] Func<CsvOptions<T>, CsvTypeMap<T, TValue>, IDematerializer<T, TValue>> valueFactory
    )
    {
        MakeReadOnly();

        if (FlameCsvGlobalOptions.CachingDisabled)
        {
            return valueFactory(this, typeMap);
        }

        TrimmingCache<MaterializerKey, object> cache = _bindingCache ?? InitializeBindingCache();
        MaterializerKey key = new(typeMap);

        if (!cache.TryGetValue(key, out object? dematerializer))
        {
            cache.Add(key, dematerializer = valueFactory(this, typeMap));
        }

        return (IDematerializer<T, TValue>)dematerializer;
    }
}
