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
    private TrimmingCache<CacheKey, object> BindingCache
    {
        get
        {
            if (FlameCsvGlobalOptions.CachingDisabled) Throw.Unreachable("Caching is disabled.");

            var local = _bindingCache;

            if (local is not null) return local;

            return Interlocked.CompareExchange(
                    ref _bindingCache,
                    new TrimmingCache<CacheKey, object>(),
                    null) ??
                _bindingCache;
        }
    }

    private TrimmingCache<CacheKey, object>? _bindingCache;

    internal IMaterializer<T, TValue> GetMaterializer<TValue>(
        ReadOnlySpan<string> headers,
        bool ignoreUnmatched,
        [RequireStaticDelegate] Func<CsvOptions<T>, ReadOnlySpan<string>, bool, IMaterializer<T, TValue>> valueFactory)
    {
        MakeReadOnly();

        if (FlameCsvGlobalOptions.CachingDisabled || !CacheKey.CanCache(headers.Length))
        {
            return valueFactory(this, headers, ignoreUnmatched);
        }

        CacheKey key = CacheKey.ForMaterializer(this, typeof(TValue), ignoreUnmatched, headers);

        if (!BindingCache.TryGetValue(key, out object? materializer))
        {
            BindingCache.Add(key, materializer = valueFactory(this, headers, ignoreUnmatched));
        }

        return (IMaterializer<T, TValue>)materializer;
    }

    internal IMaterializer<T, TValue> GetMaterializer<TValue>(
        CsvTypeMap<T, TValue> typeMap,
        ReadOnlySpan<string> headers,
        [RequireStaticDelegate]
        Func<CsvOptions<T>, CsvTypeMap<T, TValue>, ReadOnlySpan<string>, IMaterializer<T, TValue>> valueFactory)
    {
        MakeReadOnly();

        if (FlameCsvGlobalOptions.CachingDisabled || !CacheKey.CanCache(headers.Length))
        {
            return valueFactory(this, typeMap, headers);
        }

        CacheKey key = CacheKey.ForMaterializer(this, typeMap, false, headers);

        if (!BindingCache.TryGetValue(key, out object? materializer))
        {
            BindingCache.Add(key, materializer = valueFactory(this, typeMap, headers));
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

        CacheKey key = CacheKey.ForDematerializer(this, typeof(TValue));

        if (!BindingCache.TryGetValue(key, out object? dematerializer))
        {
            BindingCache.Add(key, dematerializer = valueFactory(this));
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

        CacheKey key = CacheKey.ForDematerializer(this, typeMap);

        if (!BindingCache.TryGetValue(key, out object? dematerializer))
        {
            BindingCache.Add(key, dematerializer = valueFactory(this, typeMap));
        }

        return (IDematerializer<T, TValue>)dematerializer;
    }

    private protected readonly struct CacheKey : IEquatable<CacheKey>
    {
        public static CacheKey ForMaterializer(
            CsvOptions<T> options,
            Type target,
            bool ignoreUnmatched,
            ReadOnlySpan<string> headers)
        {
            Debug.Assert(CanCache(headers.Length));
            return new(options, false, target, ignoreUnmatched, headers);
        }

        public static CacheKey ForMaterializer(
            CsvOptions<T> options,
            CsvTypeMap target,
            bool ignoreUnmatched,
            ReadOnlySpan<string> headers)
        {
            Debug.Assert(CanCache(headers.Length));
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

        public static bool CanCache(int headersLength) => (uint)headersLength <= StringScratch.MaxLength;

        private readonly bool _write;
        private readonly object _target;
        private readonly bool _ignoreUnmatched;
        private readonly StackHeaders _headers;

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
            ReadOnlySpan<string> headers)
        {
            Debug.Assert(target is CsvTypeMap or Type);
            Debug.Assert(headers.Length <= StringScratch.MaxLength);

            _write = write;
            _target = target;
            _ignoreUnmatched = ignoreUnmatched;
            _headers = new StackHeaders(options.Comparer, headers);
        }

        public bool Equals(CacheKey other)
        {
            return
                _write == other._write &&
                _ignoreUnmatched == other._ignoreUnmatched &&
                ReferenceEquals(_target, other._target) &&
                _headers.Equals(other._headers);
        }

        public override bool Equals(object? obj) => obj is CacheKey ck && Equals(ck);

        public override int GetHashCode()
        {
            HashCode hash = new();
            hash.Add(_write.GetHashCode());
            hash.Add(_target.GetHashCode());
            hash.Add(_ignoreUnmatched.GetHashCode());
            hash.Add(_headers.GetHashCode());
            return hash.ToHashCode();
        }
    }
}
