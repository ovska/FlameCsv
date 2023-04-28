using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Utilities;

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
    public CsvDialect<T> Dialect => _context.Dialect;

    internal readonly CsvReadingContext<T> _context;
    private T[]? _unescapeBuffer; // rented array for unescaping

    private ReadOnlyMemory<T>[] _fields; // cached field values by index
    private int _index;

    public Dictionary<string, int>? Header { get; set; }

    private CsvEnumerationStateRef<T> _state;
    private bool _disposed;

    private Dictionary<Type, object>? _materializerCache;
    private int? _expectedFieldCount;

    public CsvEnumerationState(in CsvReadingContext<T> context)
    {
        _context = context;
        _fields = new ReadOnlyMemory<T>[16];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Initialize(ReadOnlyMemory<T> memory, RecordMeta meta)
    {
        Throw.IfEnumerationDisposed(_disposed);

        _index = 0;
        _state = GetInitialStateFor(memory, meta);

        if (_context.ValidateFieldCount)
            ValidateFieldCount();

        return ++Version;
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
        _fields = default!;
        _state = default;
        _context.ArrayPool.EnsureReturned(ref _unescapeBuffer);

#if DEBUG
        GC.SuppressFinalize(this);
#endif
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

        field = _fields[index];
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetFieldCount()
    {
        FullyConsume();
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
            if (_fields.Length <= _index)
            {
                Array.Resize(ref _fields, _fields.Length * 2);
            }

            _fields[_index++] = field;
            return true;
        }

        return false;
    }

    public void FullyConsume()
    {
        while (TryReadNextField())
        {
        }
    }

    public ArraySegment<ReadOnlyMemory<T>> GetFields()
    {
        FullyConsume();
        return new(_fields, 0, _index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureVersion(int version)
    {
        Throw.IfEnumerationDisposed(_disposed);
        Throw.IfEnumerationChanged(version, Version);
    }

    private void ValidateFieldCount()
    {
        FullyConsume();

        if (_expectedFieldCount is null)
        {
            _expectedFieldCount = _index;
        }
        else if (_index != _expectedFieldCount.Value)
        {
            Throw.InvalidData_FieldCount(_expectedFieldCount.Value, _index);
        }
    }

#if DEBUG
    ~CsvEnumerationState()
    {
        if (!_disposed)
        {
            throw new UnreachableException("CsvEnumerationState was not disposed");
        }
    }
#endif
}

