using System.Collections.Concurrent;
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
    private int _version;

    public void EnsureVersion(int version)
    {
        if (_version != Interlocked.CompareExchange(ref _version, 0, 0))
            Throw.InvalidOp_EnumerationChanged();
    }

    public CsvHeader? Header { get; set; }

    public IDictionary<object, object> MaterializerCache => _materializerCache;

    private readonly ConcurrentDictionary<object, object> _materializerCache = [];

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
