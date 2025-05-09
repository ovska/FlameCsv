using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace FlameCsv.Tests;

[SupportedOSPlatform("windows")]
public sealed class GuardedMemoryManagerPool<T>(bool fromEnd) : MemoryPool<T>
    where T : unmanaged
{
    public bool IsDisposed { get; private set; }
    public bool FromEnd => fromEnd;
    public bool CollectStacktraces { get; init; }

    public override int MaxBufferSize { get; } = Environment.SystemPageSize * 128;

    private readonly ConcurrentDictionary<MemoryOwner, StackTrace?> _leases = new(ReferenceEqualityComparer.Instance);

    public override IMemoryOwner<T> Rent(int minBufferSize = -1)
    {
        if (minBufferSize == -1)
            minBufferSize = Environment.SystemPageSize;

        var retVal = new MemoryOwner(this, minBufferSize);
        _leases.TryAdd(retVal, CollectStacktraces ? new StackTrace(fNeedFileInfo: true) : null);
        return retVal;
    }

    protected override void Dispose(bool disposing)
    {
        if (_leases.IsEmpty)
        {
            IsDisposed = true;
            return;
        }

        var items = _leases.ToArray();

        var msg = string.Join('\n', items.Select(kvp => $"Len: {kvp.Key.RequestedLength}, ST: {kvp.Value}"));

        foreach (var item in items)
        {
            try
            {
                item.Key.Dispose();
            }
            catch (ObjectDisposedException) { }
        }

        IsDisposed = true;
        throw new InvalidOperationException($"{items.Length} IMemoryOwner(s) not disposed: {msg}");
    }

    private sealed class MemoryOwner(GuardedMemoryManagerPool<T> pool, int length) : IMemoryOwner<T>
    {
        private readonly GuardedMemoryManager<T> _manager = new(length, pool.FromEnd);

        public int RequestedLength => length;
        public Memory<T> Memory => _manager.Memory;

        public void Dispose()
        {
            ObjectDisposedException.ThrowIf(_manager.IsDisposed, _manager);

            using (_manager)
            {
                ObjectDisposedException.ThrowIf(pool.IsDisposed, pool);

                if (!pool._leases.TryRemove(this, out StackTrace? stackTrace))
                {
                    throw new InvalidOperationException(
                        $"Memory of len {length} did not originate from the same pool - {stackTrace}"
                    );
                }
            }
        }
    }
}
