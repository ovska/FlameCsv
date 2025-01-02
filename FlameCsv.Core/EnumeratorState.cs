using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Utilities;

namespace FlameCsv;

internal sealed class EnumeratorState<T> : IDisposable where T : unmanaged, IEquatable<T>
{
    public bool NeedsHeader
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _parser._options._hasHeader && Header is null;
    }

    public Dictionary<object, object> MaterializerCache => _materializerCache ??= new(ReferenceEqualityComparer.Instance);

    public int Version { get; private set; }

    public CsvOptions<T> Options => _parser._options;

    private readonly CsvParser<T> _parser;
    private T[]? _unescapeBuffer; // rented array for unescaping

    private ReadOnlyMemory<T> _record;
    private CsvRecordMeta _meta;
    private WritableBuffer<T> _fields;

    public Dictionary<string, int>? Header
    {
        get => _header;
        set
        {
            if (ReferenceEquals(_header, value))
                return;

            _header = value;
            _headerNames = value?.Keys.ToArray();
            _materializerCache?.Clear();
        }
    }

    private Dictionary<string, int>? _header;
    internal string[]? _headerNames;
    private Dictionary<object, object>? _materializerCache;
    private int? _expectedFieldCount;

    public EnumeratorState(CsvParser<T> parser)
    {
        _parser = parser;
        _fields = new WritableBuffer<T>(parser.ArrayPool);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Initialize(ReadOnlyMemory<T> memory, CsvRecordMeta meta)
    {
        Throw.IfEnumerationDisposed(Version == -1);

        _record = memory;
        _meta = meta;
        _fields.Clear();

        if (_parser._options._validateFieldCount)
            ValidateFieldCount();

        return ++Version;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (Version == -1)
            return;

        Version = -1;

        using (_parser)
        using (_fields)
        {
            _parser.ArrayPool.EnsureReturned(ref _unescapeBuffer);
        }

#if DEBUG
        GC.SuppressFinalize(this);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [MemberNotNull(nameof(Header))]
    public bool TryGetHeaderIndex(string name, out int index)
    {
        ArgumentNullException.ThrowIfNull(name);
        Throw.IfEnumerationDisposed(Version == -1);

        if (!_parser._options._hasHeader)
            Throw.NotSupported_CsvHasNoHeader();

        if (Header is null)
            Throw.InvalidOperation_HeaderNotRead();

        return Header.TryGetValue(name, out index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetAtIndex(int index, out ReadOnlyMemory<T> field)
    {
        Throw.IfEnumerationDisposed(Version == -1);

        FullyConsume();

        if (index < _fields.Length)
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
        return _fields.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetHeader(Dictionary<string, int> header)
    {
        Throw.IfEnumerationDisposed(Version == -1);

        if (!_parser._options._hasHeader)
            Throw.NotSupported_CsvHasNoHeader();

        if (Header is not null)
            Throw.Unreachable_AlreadyHasHeader();

        Header = header;
        _expectedFieldCount ??= Header.Count;
    }

    public ref WritableBuffer<T> GetFields()
    {
        if (_fields.Length == 0)
            FullyConsume();
        return ref _fields;
    }

    private void FullyConsume()
    {
        if (_fields.Length > 0)
            return;

        T[]? array = null;

        try
        {
            CsvFieldReader<T> reader = new(
                Options,
                _record,
                [],
                ref array,
                ref _meta);

            while (reader.MoveNext())
            {
                _fields.Push(reader.Current);
            }
        }
        finally
        {
            _parser.ArrayPool.EnsureReturned(ref array);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureVersion(int version)
    {
        Throw.IfEnumerationChanged(version, Version);
    }

    private void ValidateFieldCount()
    {
        FullyConsume();

        if (_expectedFieldCount is null)
        {
            _expectedFieldCount = _fields.Length;
        }
        else if (_fields.Length != _expectedFieldCount.Value)
        {
            Throw.InvalidData_FieldCount(_expectedFieldCount.Value, _fields.Length);
        }
    }

#if DEBUG
    ~EnumeratorState()
    {
        if (Version != -1)
        {
            throw new UnreachableException("CsvEnumerationState was not disposed");
        }
    }
#endif
}
