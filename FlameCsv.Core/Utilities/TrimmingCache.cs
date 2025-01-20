using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace FlameCsv.Utilities;

// https://github.com/dotnet/runtime/blob/4c224ab16d287c17dbc54236638db91266071465/src/libraries/System.Private.CoreLib/src/System/Buffers/Utilities.cs

internal static class TrimmingCache
{
    public static TrimmingCache<TKey, TValue> Create<TKey, TValue>(
        params ReadOnlySpan<KeyValuePair<TKey, TValue>> values)
        where TKey : class
    {
        var cache = new TrimmingCache<TKey, TValue>();

        foreach ((TKey key, TValue value) in values)
        {
            cache.Add(key, value);
        }

        return cache;
    }
}

[CollectionBuilder(typeof(TrimmingCache), methodName: nameof(TrimmingCache.Create))]
internal sealed class TrimmingCache<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>, IDisposable
    where TKey : class
{
    private sealed class Entry
    {
        public required TValue Value { get; set; }
        public long LastAccess { get; set; } = Environment.TickCount64;
    }

    private readonly ConcurrentDictionary<TKey, Entry> _entries;

    public TrimmingCache(IEqualityComparer<TKey>? comparer = null)
    {
        _entries = new(comparer);
        Gen2GcCallback.Register(Trim, targetObj: this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        if (!_disposed && _entries.TryGetValue(key, out var entry))
        {
            entry.LastAccess = Environment.TickCount64;
            value = entry.Value;
            return true;
        }

        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(TKey key, TValue value)
    {
        if (_disposed) return;

        _entries.AddOrUpdate(
            key,
            addValueFactory: static (_, value) => new Entry { Value = value },
            updateValueFactory: static (_, entry, value) =>
            {
                entry.Value = value;
                entry.LastAccess = Environment.TickCount64;
                return entry;
            },
            factoryArgument: value);
    }

    private bool _disposed;

    void IDisposable.Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _entries.Clear();
        }
    }

    private static bool Trim(object state)
    {
        var @this = (TrimmingCache<TKey, TValue>)state;

        if (@this._disposed)
        {
            @this._entries.Clear();
            return false;
        }

        MemoryPressure pressure = GCUtils.GetMemoryPressure();

        if (pressure == MemoryPressure.Low) return true;

        var threshold = pressure == MemoryPressure.High
            ? TimeSpan.FromSeconds(10)
            : TimeSpan.FromSeconds(60);

        var now = Environment.TickCount64;

        foreach ((TKey key, Entry entry) in @this._entries)
        {
            if (Stopwatch.GetElapsedTime(entry.LastAccess, now) > threshold)
            {
                @this._entries.TryRemove(key, out _);
            }
        }

        return true;
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        foreach ((TKey key, Entry value) in _entries) yield return new(key, value.Value);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

file enum MemoryPressure
{
    Low,
    Medium,
    High
}

file static class GCUtils
{
    public static MemoryPressure GetMemoryPressure()
    {
        const double highPressureThreshold = .90;
        const double mediumPressureThreshold = .70;

        GCMemoryInfo memoryInfo = GC.GetGCMemoryInfo();

        if (memoryInfo.MemoryLoadBytes >= memoryInfo.HighMemoryLoadThresholdBytes * highPressureThreshold)
        {
            return MemoryPressure.High;
        }

        if (memoryInfo.MemoryLoadBytes >= memoryInfo.HighMemoryLoadThresholdBytes * mediumPressureThreshold)
        {
            return MemoryPressure.Medium;
        }

        return MemoryPressure.Low;
    }
}
