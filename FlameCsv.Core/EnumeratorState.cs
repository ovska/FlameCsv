using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Utilities;
using JetBrains.Annotations;

namespace FlameCsv;

[MustDisposeResource]
internal sealed class EnumeratorState<T> : IDisposable where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Options is configured to require a header, and none is read yet.
    /// </summary>
    public bool NeedsHeader
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _parser.Options._hasHeader && Header is null;
    }

    public Dictionary<object, object> MaterializerCache
        => _materializerCache ??= new(ReferenceEqualityComparer.Instance);

    public int Version { get; private set; }

    public CsvOptions<T> Options => _parser.Options;

    private readonly CsvParser<T> _parser;

    private CsvLine<T> _record;
    private WritableBuffer<T> _fields;

    public CsvHeader? Header
    {
        get => _header;
        set
        {
            Throw.IfEnumerationDisposed(Version == -1);

            if (!_parser.Options._hasHeader)
                Throw.NotSupported_CsvHasNoHeader();

            if (EqualityComparer<CsvHeader>.Default.Equals(Header, value))
                return;

            if (Header is not null && value is not null)
                Throw.Unreachable_AlreadyHasHeader();

            _header = value;
            _expectedFieldCount = value?.Count;
            _materializerCache?.Clear();
        }
    }

    private Dictionary<object, object>? _materializerCache;
    private int? _expectedFieldCount;
    private CsvHeader? _header;

    public EnumeratorState(CsvParser<T> parser)
    {
        _parser = parser;
        _fields = new WritableBuffer<T>(parser.Allocator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Initialize(ref readonly CsvLine<T> data)
    {
        Throw.IfEnumerationDisposed(Version == -1);

        _record = data;
        _fields.Clear();

        if (_parser.Options._validateFieldCount)
            ValidateFieldCount();

        return ++Version;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (Version == -1)
            return;

        Version = -1;

        _parser.Dispose();
        _fields.Dispose();

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

        if (!_parser.Options._hasHeader)
            Throw.NotSupported_CsvHasNoHeader();

        if (Header is null)
            Throw.InvalidOperation_HeaderNotRead();

        return Header.TryGetValue(name, out index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetAtIndex(int index, out ReadOnlyMemory<T> field)
    {
        Throw.IfEnumerationDisposed(Version == -1);

        if (_fields.Length == 0)
        {
            Consume();
        }

        if (index < _fields.Length)
        {
            field = _fields[index];
            return true;
        }

        field = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetAtIndex(int index, out ReadOnlySpan<T> field)
    {
        Throw.IfEnumerationDisposed(Version == -1);

        if (_fields.Length == 0)
        {
            Consume();
        }

        if (index < _fields.Length)
        {
            field = _fields[index].Span;
            return true;
        }

        field = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetFieldCount()
    {
        if (_fields.Length == 0)
        {
            Consume();
        }

        return _fields.Length;
    }

    public ref WritableBuffer<T> GetFields()
    {
        if (_fields.Length == 0)
        {
            Consume();
        }

        return ref _fields;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Consume()
    {
        Debug.Assert(_fields.Length == 0);

        IMemoryOwner<T>? memoryOwner = null;

        CsvFieldReader<T> reader = new(
            Options,
            in _record,
            stackalloc T[Token<T>.StackLength],
            ref memoryOwner);

        try
        {
            while (reader.MoveNext())
            {
                _fields.Push(reader.Current);
            }
        }
        finally
        {
            reader.Dispose();
            memoryOwner?.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureVersion(int version)
    {
        if (Version == -1)
            Throw.ObjectDisposed_Enumeration();

        if (version != Version)
            Throw.InvalidOp_EnumerationChanged();
    }

    private void ValidateFieldCount()
    {
        if (_fields.Length == 0)
            Consume();

        if (_expectedFieldCount is null)
        {
            _expectedFieldCount = _fields.Length;
        }
        else if (_fields.Length != _expectedFieldCount.Value)
        {
            Throw.InvalidData_FieldCount(_expectedFieldCount.Value, _fields.Length);
        }
    }

    public BufferFieldReader<T> CreateFieldReader()
    {
        if (_fields.Length == 0)
            Consume();

        return _fields.CreateReader(_parser.Options, _record.Value);
    }

    public ReadOnlyMemory<T>[] PreserveFields()
    {
        if (_fields.Length == 0)
            Consume();

        return _fields.Preserve();
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
