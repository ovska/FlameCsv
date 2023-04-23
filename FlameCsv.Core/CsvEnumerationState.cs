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
    public int TotalFieldLength { get; private set; }
    public CsvDialect<T> Dialect => _dialect;

    private readonly CsvDialect<T> _dialect;
    private readonly CsvReaderOptions<T> _options;
    private readonly ArrayPool<T> _arrayPool; // pool used for unescape buffer
    private T[]? _unescapeBuffer; // rented array for unescaping

    private ReadOnlyMemory<T>[] _values; // cached column values by index
    private int _index; // how many values have been read

    private readonly bool _hasHeader;
    internal Dictionary<string, int>? _header;

    private CsvEnumerationStateRef<T> _state;
    private bool _disposed;

    public CsvEnumerationState(CsvReaderOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.MakeReadOnly();

        _options = options;
        _dialect = new CsvDialect<T>(options);
        _values = new ReadOnlyMemory<T>[32];
        _arrayPool = options.ArrayPool.AllocatingIfNull();
        _hasHeader = options.HasHeader;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Initialize(ReadOnlyMemory<T> memory, RecordMeta meta)
    {
        if (_disposed)
            ThrowHelper.ThrowObjectDisposedException(null, "The CSV enumeration has been disposed.");

        _index = 0;
        _values.AsSpan().Clear();
        _state = GetInitialStateFor(memory, meta);
        return ++Version;
    }

    public CsvEnumerationStateRef<T> GetInitialStateFor(ReadOnlyMemory<T> memory, RecordMeta? meta = null)
    {
        if (_disposed)
            ThrowHelper.ThrowObjectDisposedException(null, "The CSV enumeration has been disposed.");

        return new(
            dialect: in _dialect,
            record: memory,
            remaining: memory,
            isAtStart: true,
            meta: meta ?? _dialect.GetRecordMeta(memory, _options.AllowContentInExceptions),
            array: ref _unescapeBuffer,
            arrayPool: _arrayPool,
            exposeContent: _options.AllowContentInExceptions);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Version = -1;
        _values = default!;
        _state = default;
        _arrayPool.EnsureReturned(ref _unescapeBuffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetHeaderIndex(string name, out int index)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (_disposed)
            ThrowHelper.ThrowObjectDisposedException(null, "The CSV enumeration has been disposed.");

        if (!_hasHeader)
            ThrowHelper.ThrowNotSupportedException("The current CSV does not have a header record.");

        if (_header is null)
            ThrowHelper.ThrowInvalidOperationException("CSV header has not been read.");

        return _header.TryGetValue(name, out index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetAtIndex(int index, out ReadOnlyMemory<T> column)
    {
        if (_disposed)
            ThrowHelper.ThrowObjectDisposedException(null, "The CSV enumeration has been disposed.");

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
        if (_disposed)
            ThrowHelper.ThrowObjectDisposedException(null, "The CSV enumeration has been disposed.");

        if (!_hasHeader)
            ThrowHelper.ThrowNotSupportedException("The current CSV does not have a header record.");

        if (_header is not null)
            ThrowHelper.ThrowInvalidOperationException("CSV header has already been read.");

        _header = header;
    }

    private bool TryReadNextColumn()
    {
        if (_disposed)
            ThrowHelper.ThrowObjectDisposedException(null, "The CSV enumeration has been disposed.");

        if (_state.TryGetField(out ReadOnlyMemory<T> field))
        {
            if (_index >= _values.Length)
                Array.Resize(ref _values, _values.Length * 2);

            _values[_index] = field;
            _index++;
            TotalFieldLength += field.Length;
            return true;
        }

        return false;
    }

    public void EnsureVersion(int version)
    {
        if (version != Version)
            ThrowHelper.ThrowInvalidOperationException("The CSV enumeration state has been modified.");
    }
}

