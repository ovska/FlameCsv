using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;

namespace FlameCsv.Reading.Internal;

internal interface IRecordOwner
{
    void EnsureVersion(int version);
    CsvHeader? Header { get; }
    IDictionary<object, object> MaterializerCache { get; }
}

internal sealed class ParallelEnumerationOwner : IRecordOwner, IDisposable
{
    public int Version => Interlocked.CompareExchange(ref _version, 0, 0);

    private int _version;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureVersion(int version)
    {
        if (_version != Interlocked.CompareExchange(ref _version, 0, 0))
            Throw.InvalidOp_EnumerationChanged();
    }

    public CsvHeader? Header { get; set; }

    public IDictionary<object, object> MaterializerCache => _materializerCache;

    private readonly ConcurrentDictionary<object, object> _materializerCache = [];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int NextVersion()
    {
        ObjectDisposedException.ThrowIf(_version == -1, this);
        return Interlocked.Increment(ref _version);
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _version, -1);
    }
}
