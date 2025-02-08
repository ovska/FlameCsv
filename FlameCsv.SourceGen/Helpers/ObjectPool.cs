// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// define TRACE_LEAKS to get additional diagnostics that can lead to the leak sources. note: it will
// make everything about 2-3x slower
//

#define TRACE_LEAKS

// define DETECT_LEAKS to detect possible leaks
#if DEBUG
#define DETECT_LEAKS //for now always enable DETECT_LEAKS in debug.
#endif
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;

namespace FlameCsv.SourceGen.Helpers;

internal static class ObjectPool
{
    public static EquatableArray<T> ToEquatableArrayAndFree<T>(this HashSet<T> set) where T : IEquatable<T?>
    {
        var result = set.Count == 0 ? [] : set.ToEquatableArray();
        PooledSet<T>.Release(set);
        return result;
    }

    public static EquatableArray<T> ToEquatableArrayAndFree<T>(this List<T> list) where T : IEquatable<T?>
    {
        var result = list.Count == 0 ? [] : list.ToEquatableArray();
        PooledList<T>.Release(list);
        return result;
    }
}

internal class ObjectPool<T> where T : class
{
    [DebuggerDisplay("{Value,nq}")]
    private struct Element
    {
        internal T? Value;
    }

    private T? _firstItem;
    private readonly Element[] _items;
    private readonly Func<T> _factory;


    internal ObjectPool(Func<T> factory, int size = 16)
    {
        _factory = factory;
        _items = new Element[size - 1];
    }

    private T CreateInstance()
    {
        var inst = _factory();
        return inst;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal T Allocate()
    {
        var inst = _firstItem;
        if (inst == null || inst != Interlocked.CompareExchange(ref _firstItem, null, inst))
        {
            inst = AllocateSlow();
        }

#if DETECT_LEAKS
        var tracker = new LeakTracker();
        _leakTrackers.Add(inst, tracker);

#if TRACE_LEAKS
        var frame = CaptureStackTrace();
        tracker.Trace = frame;
#endif
#endif

        return inst;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private T AllocateSlow()
    {
        var items = _items;

        for (var i = 0; i < items.Length; i++)
        {
            // Note that the initial read is optimistically not synchronized. That is intentional.
            // We will interlock only when we have a candidate. in a worst case we may miss some
            // recently returned objects. Not a big deal.
            var inst = items[i].Value;
            if (inst != null)
            {
                if (inst == Interlocked.CompareExchange(ref items[i].Value, null, inst))
                {
                    return inst;
                }
            }
        }

        return CreateInstance();
    }

    /// <summary>
    /// Returns objects to the pool.
    /// </summary>
    /// <remarks>
    /// Search strategy is a simple linear probing which is chosen for it cache-friendliness.
    /// Note that Free will try to store recycled objects close to the start thus statistically
    /// reducing how far we will typically search in Allocate.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Free(T obj)
    {
        Validate(obj);
        ForgetTrackedObject(obj);

        if (_firstItem == null)
        {
            // Intentionally not using interlocked here.
            // In a worst case scenario two objects may be stored into same slot.
            // It is very unlikely to happen and will only mean that one of the objects will get collected.
            _firstItem = obj;
        }
        else
        {
            FreeSlow(obj);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void FreeSlow(T obj)
    {
        var items = _items;
        for (var i = 0; i < items.Length; i++)
        {
            if (items[i].Value == null)
            {
                items[i].Value = obj;
                break;
            }
        }
    }

    [Conditional("DEBUG")]
    [SuppressMessage("ReSharper", "UnusedParameter.Global")]
    internal void ForgetTrackedObject(T old, T? replacement = null)
    {
#if DETECT_LEAKS
        if (_leakTrackers.TryGetValue(old, out LeakTracker tracker))
        {
            tracker.Dispose();
            _leakTrackers.Remove(old);
        }
        else
        {
            var trace = CaptureStackTrace();
            Debug.WriteLine(
                $"TRACEOBJECTPOOLLEAKS_BEGIN\nObject of type {typeof(T)} was freed, but was not from pool. \n Callstack: \n {trace} TRACEOBJECTPOOLLEAKS_END");
        }

        if (replacement != null)
        {
            tracker = new LeakTracker();
            _leakTrackers.Add(replacement, tracker);
        }
#endif
    }

#if DETECT_LEAKS
    // ReSharper disable once StaticMemberInGenericType
    private static readonly Lazy<Type> _stackTraceType = new(() => Type.GetType("System.Diagnostics.StackTrace"));

    private static object CaptureStackTrace()
    {
        return Activator.CreateInstance(_stackTraceType.Value);
    }
#endif

    [Conditional("DEBUG")]
    private void Validate(object obj)
    {
        Debug.Assert(obj != null, "freeing null?");

        Debug.Assert(_firstItem != obj, "freeing twice?");

        var items = _items;
        for (var i = 0; i < items.Length; i++)
        {
            var value = items[i].Value;
            if (value == null)
            {
                return;
            }

            Debug.Assert(value != obj, "freeing twice?");
        }
    }

#if DETECT_LEAKS
    private static readonly ConditionalWeakTable<T, LeakTracker> _leakTrackers = new();

    private sealed class LeakTracker : CriticalFinalizerObject, IDisposable
    {
        private volatile bool _disposed;

#if TRACE_LEAKS
        internal volatile object? Trace;
#endif

        public void Dispose()
        {
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        private string GetTrace()
        {
#if TRACE_LEAKS
            return Trace == null ? "" : Trace.ToString();
#else
            return "Leak tracing information is disabled. Define TRACE_LEAKS on ObjectPool`1.cs to get more info \n";
#endif
        }

        ~LeakTracker()
        {
            if (!this._disposed &&
#pragma warning disable RS1035
                !Environment.HasShutdownStarted)
#pragma warning restore RS1035
            {
                // If you are seeing this message it means that object has been allocated from the pool
                // and has not been returned back. This is not critical, but turns pool into rather
                // inefficient kind of "new".
                Debug.WriteLine(
                    $"TRACEOBJECTPOOLLEAKS_BEGIN\nPool detected potential leaking of {typeof(T)}. \n Location of the leak: \n {GetTrace()} TRACEOBJECTPOOLLEAKS_END");
            }
        }
    }
#endif
}
