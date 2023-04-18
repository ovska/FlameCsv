using System.Buffers;
using System.Collections;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;

namespace FlameCsv;

public struct CsvRecordEnumerator<T> : IEnumerator<ReadOnlyMemory<T>> where T : unmanaged, IEquatable<T>
{
    private readonly CsvEnumerationState<T> _state;
    private readonly int _version;
    private readonly bool _disposeSource;
    private int _index;

    public ReadOnlyMemory<T> Current { get; private set; }
    object IEnumerator.Current => Current;

    /// <summary>
    /// Initializes a new field enumerator for the specified CSV record.
    /// </summary>
    /// <remarks>
    /// Creating an enumerator this way always causes an allocation for the data. To read CSV records with more efficient
    /// memory usage, use the GetEnumerable-methods on <see cref="CsvReader"/>.
    /// </remarks>
    /// <param name="data">Complete CSV record without trailing newline</param>
    /// <param name="dialect">Dialect used to parse the fields</param>
    /// <param name="arrayPool">
    /// Pool used to unescape possible quoted fields. Set to null to allocate instead. If set, remember to dispose the enumerator
    /// either via <c>foreach</c> or explicitly.
    /// </param>
    public CsvRecordEnumerator(ReadOnlyMemory<T> data, CsvDialect<T> dialect, ArrayPool<T>? arrayPool = null)
    {
        dialect.EnsureValid();

        CsvEnumerationState<T> state = new(dialect, arrayPool);
        state.Initialize(data, data.Span.Count(dialect.Quote));

        _state = state;
        _disposeSource = true;
        _version = state.Version;
        _index = 0;
    }

    internal CsvRecordEnumerator(CsvEnumerationState<T> source)
    {
        _state = source;
        _version = source.Version;
        _index = 0;
    }

    void IDisposable.Dispose()
    {
        if (_disposeSource)
            _state.Dispose();
    }

    public bool MoveNext()
    {
        if (_version != _state.Version)
            ThrowHelper.ThrowInvalidOperationException("The enumerator cannot be used after the next record has been read.");

        if (_state.TryGetAtIndex(_index, out ReadOnlyMemory<T> column))
        {
            Current = column;
            _index++;
            return true;
        }

        return false;
    }

    public void Reset() => _index = 0;
}

