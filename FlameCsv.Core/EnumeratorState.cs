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
        get => Options._hasHeader && Header is null;
    }

    public Dictionary<object, object> MaterializerCache
        => _materializerCache ??= new(ReferenceEqualityComparer.Instance);

    public int Version { get; private set; }

    public CsvOptions<T> Options { get; }

    private ReadOnlyMemory<T> _record;
    private WritableBuffer<T> _fields;

    public CsvHeader? Header
    {
        get => _header;
        set
        {
            Throw.IfEnumerationDisposed(Version == -1);

            if (!Options._hasHeader && value is not null)
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

    internal bool ContainsHeader(string name) => _header?.ContainsKey(name) ?? false;

    public EnumeratorState(CsvOptions<T> options)
    {
        Debug.Assert(options.IsReadOnly);
        Options = options;
        _fields = new WritableBuffer<T>(options._memoryPool);

        HotReloadService.RegisterForHotReload(
            this,
            static state => ((EnumeratorState<T>)state)._materializerCache?.Clear());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Initialize(ref readonly CsvLine<T> line)
    {
        Throw.IfEnumerationDisposed(Version == -1);

        _fields.Clear();

        Span<T> unescapeBuffer = stackalloc T[Token<T>.StackLength];
        MetaFieldReader<T> reader = new(in line, unescapeBuffer);

        for (int i = 0; i < reader.FieldCount; i++)
        {
            _fields.Push(reader[i]);
        }

        _record = line.Data;

        if (Options._validateFieldCount)
            ValidateFieldCount();

        return ++Version;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (Version == -1)
            return;

        Version = -1;
        _fields.Dispose();
        _materializerCache = null;
        HotReloadService.UnregisterForHotReload(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [MemberNotNull(nameof(Header))]
    public bool TryGetHeaderIndex(string name, out int index)
    {
        ArgumentNullException.ThrowIfNull(name);
        Throw.IfEnumerationDisposed(Version == -1);

        if (!Options._hasHeader)
            Throw.NotSupported_CsvHasNoHeader();

        if (Header is null)
            Throw.InvalidOperation_HeaderNotRead();

        return Header.TryGetValue(name, out index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetAtIndex(int index, out ReadOnlyMemory<T> field)
    {
        Throw.IfEnumerationDisposed(Version == -1);

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
        return _fields.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref WritableBuffer<T> GetFields()
    {
        return ref _fields;
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
        return _fields.CreateReader(Options, _record);
    }

    public ReadOnlyMemory<T>[] PreserveFields()
    {
        return _fields.Preserve();
    }
}
