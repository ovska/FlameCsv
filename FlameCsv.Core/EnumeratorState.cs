using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv;

internal sealed class EnumeratorState<T> : IDisposable where T : unmanaged, IEquatable<T>
{
    public bool NeedsHeader
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _context.HasHeader && Header is null;
    }

    public Dictionary<object, object> MaterializerCache => _materializerCache ??= [];

    public int Version { get; private set; }
    public CsvDialect<T> Dialect => _context.Dialect;

    internal readonly CsvReadingContext<T> _context;
    private T[]? _unescapeBuffer; // rented array for unescaping

    private ReadOnlyMemory<T> _record;
    private RecordMeta _meta;
    private List<ReadOnlyMemory<T>>? _fields;

    public Dictionary<string, int>? Header
    {
        get => _header;
        set
        {
            _header = value;
            _materializerCache?.Clear();

            if (value is null)
            {
                _headerNames = null;
            }
            else
            {
                _headerNames = new string[value.Count];
                int ix = 0;
                foreach (var (header, _) in value)
                {
                    _headerNames[ix++] = header;
                }
            }
        }
    }

    private bool _disposed;

    private Dictionary<string, int>? _header;
    internal string[]? _headerNames;
    private Dictionary<object, object>? _materializerCache;
    private int? _expectedFieldCount;

    public EnumeratorState(in CsvReadingContext<T> context)
    {
        _context = context;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Initialize(ReadOnlyMemory<T> memory, RecordMeta meta)
    {
        Throw.IfEnumerationDisposed(_disposed);

        _record = memory;
        _meta = meta;
        _fields = null;

        if (_context.ValidateFieldCount)
            ValidateFieldCount();

        return ++Version;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Version = -1;
        _fields = default!;
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

        FullyConsume();

        if (index < _fields.Count)
        {
            field = _fields[index];
            return true;
        }

        field = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetFieldCount()
    {
        FullyConsume();
        return _fields.Count;
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

    public List<ReadOnlyMemory<T>> GetFields()
    {
        FullyConsume();
        return _fields;
    }

    [MemberNotNull(nameof(_fields))]
    private void FullyConsume()
    {
        if (_fields is not null)
            return;

        _fields = [];

        T[]? array = null;

        try
        {
            var meta = _context.GetRecordMeta(_record);
            CsvFieldReader<T> reader = new(
                _record,
                in _context,
                [],
                ref array,
                meta.quoteCount,
                meta.escapeCount);

            while (reader.TryReadNext(out ReadOnlyMemory<T> field))
            {
                if (field.Span.Overlaps(array))
                {
                    _fields.Add(field.ToArray());
                }
                else
                {
                    _fields.Add(field);
                }
            }
        }
        finally
        {
            _context.ArrayPool.EnsureReturned(ref array);
        }
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
            _expectedFieldCount = _fields.Count;
        }
        else if (_fields.Count != _expectedFieldCount.Value)
        {
            Throw.InvalidData_FieldCount(_expectedFieldCount.Value, _fields.Count);
        }
    }

#if DEBUG
    ~EnumeratorState()
    {
        if (!_disposed)
        {
            throw new UnreachableException("CsvEnumerationState was not disposed");
        }
    }
#endif
}

