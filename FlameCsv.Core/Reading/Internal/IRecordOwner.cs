using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using FlameCsv.Utilities;

namespace FlameCsv.Reading.Internal;

internal interface IRecordOwner<T> where T : unmanaged
{
    void EnsureVersion(int version);
    CsvHeader? Header { get; }
    IDictionary<object, object> MaterializerCache { get; }
    Allocator<T>? GetAllocator(int fieldIndex);
}

internal sealed class ParallelEnumerationOwner<T> : IRecordOwner<T>, IDisposable where T : unmanaged
{
    public int Version => Interlocked.CompareExchange(ref _version, 0, 0);
    public CsvHeader? Header { get; set; }
    public IDictionary<object, object> MaterializerCache => _materializerCache;

    public Allocator<T>? GetAllocator(int fieldIndex)
    {
        ObjectDisposedException.ThrowIf(_version == -1, this);
        ArgumentOutOfRangeException.ThrowIfNegative(fieldIndex);
        return _allocators[fieldIndex];
    }

    private readonly ConcurrentDictionary<object, object> _materializerCache = [];
    private readonly PerColumnAllocator<T> _allocators;
    private int _version;

    public ParallelEnumerationOwner(MemoryPool<T> memoryPool)
    {
        _allocators = new(memoryPool);
        HotReloadService.RegisterForHotReload(
            this,
            static state => ((ParallelEnumerationOwner<T>)state)._materializerCache.Clear());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureVersion(int version)
    {
        if (_version != Interlocked.CompareExchange(ref _version, 0, 0))
            Throw.InvalidOp_EnumerationChanged();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int NextVersion()
    {
        ObjectDisposedException.ThrowIf(_version == -1, this);
        return Interlocked.Increment(ref _version);
    }

    public void Dispose()
    {
        while (Interlocked.Exchange(ref _version, -1) != -1) ;
        _allocators.Dispose();
        _materializerCache.Clear();
    }
}
