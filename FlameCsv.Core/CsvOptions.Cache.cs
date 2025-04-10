using System.Collections.Immutable;
using System.Diagnostics;
using FlameCsv.Binding;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Utilities;
using FlameCsv.Writing;
using JetBrains.Annotations;

namespace FlameCsv;

public partial class CsvOptions<T>
{
    private TrimmingCache<CacheKey, object>? _bindingCache;

    private TrimmingCache<CacheKey, object> GetBindingCache()
    {
        if (FlameCsvGlobalOptions.CachingDisabled) Throw.Unreachable("Caching is disabled.");

        var local = _bindingCache;

        if (local is not null) return local;

        TrimmingCache<CacheKey, object> instance = new();

        if (Interlocked.CompareExchange(ref _bindingCache, instance, null) is not null)
        {
            // Another thread already initialized the cache.
            // Dispose of the new instance.
            ((IDisposable)instance).Dispose();
        }

        return _bindingCache = instance;
    }

    internal IMaterializer<T, TValue> GetMaterializer<TValue>(
        ImmutableArray<string> headers,
        bool ignoreUnmatched,
        [RequireStaticDelegate] Func<CsvOptions<T>, ImmutableArray<string>, bool, IMaterializer<T, TValue>> valueFactory)
    {
        MakeReadOnly();

        if (FlameCsvGlobalOptions.CachingDisabled)
        {
            return valueFactory(this, headers, ignoreUnmatched);
        }

        TrimmingCache<CacheKey, object> cache = GetBindingCache();
        CacheKey key = CacheKey.ForMaterializer(this, typeof(TValue), ignoreUnmatched, headers);

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
        Func<CsvOptions<T>, CsvTypeMap<T, TValue>, ImmutableArray<string>, IMaterializer<T, TValue>> valueFactory)
    {
        MakeReadOnly();

        if (FlameCsvGlobalOptions.CachingDisabled)
        {
            return valueFactory(this, typeMap, headers);
        }

        TrimmingCache<CacheKey, object> cache = GetBindingCache();
        CacheKey key = CacheKey.ForMaterializer(this, typeMap, false, headers);

        if (!cache.TryGetValue(key, out object? materializer))
        {
            cache.Add(key, materializer = valueFactory(this, typeMap, headers));
        }

        return (IMaterializer<T, TValue>)materializer;
    }

    internal IDematerializer<T, TValue> GetDematerializer<TValue>(
        [RequireStaticDelegate] Func<CsvOptions<T>, IDematerializer<T, TValue>> valueFactory)
    {
        MakeReadOnly();

        if (FlameCsvGlobalOptions.CachingDisabled)
        {
            return valueFactory(this);
        }

        TrimmingCache<CacheKey, object> cache = GetBindingCache();
        CacheKey key = CacheKey.ForDematerializer(this, typeof(TValue));

        if (!cache.TryGetValue(key, out object? dematerializer))
        {
            cache.Add(key, dematerializer = valueFactory(this));
        }

        return (IDematerializer<T, TValue>)dematerializer;
    }

    internal IDematerializer<T, TValue> GetDematerializer<TValue>(
        CsvTypeMap<T, TValue> typeMap,
        [RequireStaticDelegate] Func<CsvOptions<T>, CsvTypeMap<T, TValue>, IDematerializer<T, TValue>> valueFactory)
    {
        MakeReadOnly();

        if (FlameCsvGlobalOptions.CachingDisabled)
        {
            return valueFactory(this, typeMap);
        }

        TrimmingCache<CacheKey, object> cache = GetBindingCache();
        CacheKey key = CacheKey.ForDematerializer(this, typeMap);

        if (!cache.TryGetValue(key, out object? dematerializer))
        {
            cache.Add(key, dematerializer = valueFactory(this, typeMap));
        }

        return (IDematerializer<T, TValue>)dematerializer;
    }

    private protected readonly struct CacheKey : IEquatable<CacheKey>
    {
        public static CacheKey ForMaterializer(
            CsvOptions<T> options,
            Type target,
            bool ignoreUnmatched,
            ImmutableArray<string> headers)
        {
            return new(options, false, target, ignoreUnmatched, headers);
        }

        public static CacheKey ForMaterializer(
            CsvOptions<T> options,
            CsvTypeMap target,
            bool ignoreUnmatched,
            ImmutableArray<string> headers)
        {
            return new(options, false, target, ignoreUnmatched, headers);
        }

        public static CacheKey ForDematerializer(CsvOptions<T> options, Type target)
        {
            return new(options, true, target, false, []);
        }

        public static CacheKey ForDematerializer(CsvOptions<T> options, CsvTypeMap target)
        {
            return new(options, true, target, false, []);
        }

        private readonly bool _write;
        private readonly object _target;
        private readonly bool _ignoreUnmatched;
        private readonly IEqualityComparer<string> _comparer;
        private readonly ImmutableArray<string> _headers;

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheKey"/> class.
        /// </summary>
        /// <param name="options">Current options instance</param>
        /// <param name="write"></param>
        /// <param name="target">Either the target type or a typemap instance</param>
        /// <param name="ignoreUnmatched"></param>
        /// <param name="headers"></param>
        private CacheKey(
            CsvOptions<T> options,
            bool write,
            object target,
            bool ignoreUnmatched,
            ImmutableArray<string> headers)
        {
            Debug.Assert(target is CsvTypeMap or Type);

            _write = write;
            _target = target;
            _ignoreUnmatched = ignoreUnmatched;
            _comparer = options.Comparer;
            _headers = headers;
        }

        public bool Equals(CacheKey other)
        {
            return
                _write == other._write &&
                _ignoreUnmatched == other._ignoreUnmatched &&
                ReferenceEquals(_target, other._target) &&
                ReferenceEquals(_comparer, other._comparer) &&
                _headers.AsSpan().SequenceEqual(other._headers.AsSpan(), _comparer);
        }

        public override bool Equals(object? obj) => obj is CacheKey ck && Equals(ck);

        public override int GetHashCode()
        {
            HashCode hash = new();
            hash.Add(_write);
            hash.Add(_target);
            hash.Add(_ignoreUnmatched);
            hash.Add(_comparer);
            hash.Add(_headers.Length);
            foreach (var header in _headers) hash.Add(header, _comparer);
            return hash.ToHashCode();
        }
    }
}
