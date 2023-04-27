using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv;

internal sealed class CsvEnumerationState<T> : IDisposable where T : unmanaged, IEquatable<T>
{
    public bool NeedsHeader
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _context.HasHeader && Header is null;
    }

    public Dictionary<Type, object> MaterializerCache => _materializerCache ??= new();

    public int Version { get; private set; }
    public int TotalFieldLength { get; private set; }
    public CsvDialect<T> Dialect => _context.Dialect;

    internal readonly CsvReadingContext<T> _context;
    private T[]? _unescapeBuffer; // rented array for unescaping

    private ReadOnlyMemory<T>[] _values; // cached field values by index
    private int _index; // how many values have been read

    public Dictionary<string, int>? Header { get; set; }

    private CsvEnumerationStateRef<T> _state;
    private bool _disposed;

    private Dictionary<Type, object>? _materializerCache;
    internal int? _expectedFieldCount;

    public CsvEnumerationState(in CsvReadingContext<T> context)
    {
        _context = context;
        _values = new ReadOnlyMemory<T>[32];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Initialize(ReadOnlyMemory<T> memory, RecordMeta meta)
    {
        Throw.IfEnumerationDisposed(_disposed);

        _index = 0;
        _values.AsSpan().Clear();
        _state = GetInitialStateFor(memory, meta);
        int newVersion = ++Version;

        if (_context.ValidateFieldCount)
            ValidateFieldCountForCurrent();

        return newVersion;
    }

    public CsvEnumerationStateRef<T> GetInitialStateFor(ReadOnlyMemory<T> memory, RecordMeta? meta = null)
    {
        Throw.IfEnumerationDisposed(_disposed);

        return new CsvEnumerationStateRef<T>(in _context, memory, ref _unescapeBuffer, meta);
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
        _context.ArrayPool.EnsureReturned(ref _unescapeBuffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [MemberNotNull(nameof(Header))]
    public bool TryGetHeaderIndex(string name, out int index)
    {
        ArgumentNullException.ThrowIfNull(name);
        Throw.IfEnumerationDisposed(_disposed);

        if (!_context.HasHeader)
            Throw.NotSupported_CsvHasNoHeader();

        if (Header is null)
            Throw.InvalidOperation_HeaderNotRead();

        return Header.TryGetValue(name, out index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetAtIndex(int index, out ReadOnlyMemory<T> field)
    {
        Throw.IfEnumerationDisposed(_disposed);

        while (_index <= index)
        {
            if (!TryReadNextField())
            {
                field = default;
                return false;
            }
        }

        field = _values[index];
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetFieldCount()
    {
        while (TryReadNextField())
        { }

        return _index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetHeader(Dictionary<string, int> header)
    {
        Throw.IfEnumerationDisposed(_disposed);

        if (!_context.HasHeader)
            Throw.NotSupported_CsvHasNoHeader();

        if (Header is not null)
            Throw.Unreachable_AlreadyHasHeader();

        Header = header;
        _expectedFieldCount ??= Header.Count;
    }

    private bool TryReadNextField()
    {
        Throw.IfEnumerationDisposed(_disposed);

        if (_context.TryGetField(ref _state, out ReadOnlyMemory<T> field))
        {
            if (_index >= _values.Length)
                Array.Resize(ref _values, _values.Length * 2);

            _values[_index] = field;
            _index++;
            TotalFieldLength += field.Length;
            return true;
        }

        //ThrowHelper.ThrowCsvException("The CSV record has an invalid number of fields.", _state.Meta)

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureVersion(int version)
    {
        Throw.IfEnumerationDisposed(_disposed);
        Throw.IfEnumerationChanged(version, Version);
    }

    private void ValidateFieldCountForCurrent()
    {
        while (TryReadNextField())
        {
        }

        if (_index != (_expectedFieldCount ??= _index))
            Throw.InvalidData_FieldCount(_expectedFieldCount.Value, _index);

        _expectedFieldCount = _index;
    }
}

