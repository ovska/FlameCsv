using System.Buffers;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv;

internal sealed class CsvEnumerationState<T> : IDisposable where T : unmanaged, IEquatable<T>
{
    public bool NeedsHeader
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _hasHeader && _header is null;
    }

    public int Version { get; private set; }
    public CsvDialect<T> Dialect => _dialect;

    private readonly CsvDialect<T> _dialect;
    private readonly bool _exposeContents; // allow content in exceptions

    private readonly ArrayPool<T> _arrayPool; // pool used for unescape buffer
    private T[]? _unescapeBuffer; // rented array for unescaping

    private ReadOnlyMemory<T>[] _values; // cached column values by index
    private int _index; // how many values have been read

    private ReadOnlyMemory<T> _record; // whole record's raw data
    private ReadOnlyMemory<T> _remaining; // unread data, if empty, the whole record has been read
    private uint _quotesRemaining; // how many quotes are left in the remaining data
    private uint _escapesRemaining; // how many escapes are left in the remaining data

    private readonly bool _hasHeader;
    internal Dictionary<string, int>? _header;

    public CsvEnumerationState(CsvReaderOptions<T> options) : this(new CsvDialect<T>(options), options.ArrayPool)
    {
        options.MakeReadOnly();
        _hasHeader = options.HasHeader;
        _exposeContents = options.AllowContentInExceptions;
    }

    public CsvEnumerationState(CsvDialect<T> dialect, ArrayPool<T>? arrayPool)
    {
        _dialect = dialect;
        _arrayPool = arrayPool.AllocatingIfNull();
        _values = new ReadOnlyMemory<T>[32];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Initialize(ReadOnlyMemory<T> memory, RecordMeta meta)
    {
        Version++;
        _index = 0;
        _values.AsSpan().Clear();
        _record = memory;
        _remaining = memory;
        _quotesRemaining = meta.quoteCount;
        _escapesRemaining = meta.escapeCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        Version = -1;
        _values = default!;
        _record = default;
        _remaining = default;
        _arrayPool.EnsureReturned(ref _unescapeBuffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetHeaderIndex(string name, out int index)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (!_hasHeader)
            ThrowHelper.ThrowNotSupportedException("The current CSV does not have a header record.");

        if (_header is null)
            ThrowHelper.ThrowInvalidOperationException("CSV header has not been read.");

        return _header.TryGetValue(name, out index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetAtIndex(int index, out ReadOnlyMemory<T> column)
    {
        while (_index <= index)
        {
            if (!TryReadNextColumn())
            {
                column = default;
                return false;
            }
        }

        column = _values[index];
        return true;
    }

    public int GetFieldCount()
    {
        while (TryReadNextColumn())
        { }

        return _index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetHeader(Dictionary<string, int> header)
    {
        if (!_hasHeader)
            ThrowHelper.ThrowNotSupportedException("The current CSV does not have a header record.");

        if (_header is not null)
            ThrowHelper.ThrowInvalidOperationException("CSV header has already been read.");

        _header = header;
    }

    private bool TryReadNextColumn()
    {
        CsvEnumerationStateRef<T> state = new(
            dialect: in _dialect,
            record: _record,
            remaining: _remaining,
            isAtStart: _index == 0,
            meta: new RecordMeta { quoteCount = _quotesRemaining, escapeCount = _escapesRemaining },
            array: ref _unescapeBuffer,
            arrayPool: _arrayPool,
            exposeContent: _exposeContents);

        if (state.TryGetField(out ReadOnlyMemory<T> field))
        {
            if (_index >= _values.Length)
                Array.Resize(ref _values, _values.Length * 2);

            _values[_index++] = field;
            _remaining = state.remaining;
            _quotesRemaining = state.quotesRemaining;
            _escapesRemaining = state.escapesRemaining;
            return true;
        }

        return false;
    }
}

