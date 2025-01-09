using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Runtime;
using FlameCsv.Utilities;

namespace FlameCsv;

/// <summary>
/// Represents the current record when reading CSV.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
[DebuggerTypeProxy(typeof(CsvValueRecord<>.CsvRecordDebugView))]
public readonly struct CsvValueRecord<T> : ICsvRecord<T>, IEnumerable<ReadOnlyMemory<T>>
    where T : unmanaged, IBinaryInteger<T>
{
    public long Position { get; }
    public int Line { get; }

    public ReadOnlyMemory<T> RawRecord
    {
        get
        {
            _state.EnsureVersion(_version);
            return _record;
        }
    }

    /// <summary>
    /// Whether the current CSV enumeration has a header.
    /// </summary>
    public bool HasHeader => _state.Header is not null;

    public ReadOnlySpan<string> Header
    {
        get
        {
            _state.EnsureVersion(_version);

            if (!_state.Options._hasHeader)
                Throw.NotSupported_CsvHasNoHeader();

            if (_state.Header is null)
                Throw.InvalidOperation_HeaderNotRead();

            return _state.Header.Values;
        }
    }

    public ReadOnlyMemory<T> this[int index] => GetField(index);
    public ReadOnlyMemory<T> this[string name] => GetField(name);

    internal readonly EnumeratorState<T> _state;
    internal readonly CsvOptions<T> _options;

    private readonly int _version;
    private readonly ReadOnlyMemory<T> _record;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvValueRecord(
        long position,
        int line,
        ref readonly CsvLine<T> data,
        CsvOptions<T> options,
        EnumeratorState<T> state)
    {
        Position = position;
        Line = line;
        _record = data.Value;
        _options = options;
        _state = state;
        _version = _state.Initialize(in data);
    }

    /// <inheritdoc cref="ICsvRecord{T}.GetField(string)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyMemory<T> GetField(string name)
    {
        _state.EnsureVersion(_version);

        if (!_state.TryGetHeaderIndex(name, out int index))
        {
            Throw.Argument_HeaderNameNotFound(name, _options.AllowContentInExceptions, _state.Header.Keys);
        }

        if (!_state.TryGetAtIndex(index, out ReadOnlyMemory<T> field))
        {
            Throw.Argument_FieldIndex(index, _state, name);
        }

        return field;
    }

    /// <inheritdoc cref="ICsvRecord{T}.GetField(int)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyMemory<T> GetField(int index)
    {
        _state.EnsureVersion(_version);

        if (!_state.TryGetAtIndex(index, out ReadOnlyMemory<T> field))
        {
            Throw.Argument_FieldIndex(index, _state);
        }

        return field;
    }

    /// <inheritdoc cref="ICsvRecord{T}.GetFieldCount"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetFieldCount()
    {
        _state.EnsureVersion(_version);

        return _state.GetFieldCount();
    }

    public bool TryGetValue<TValue>(int index, [MaybeNullWhen(false)] out TValue value)
    {
        _state.EnsureVersion(_version);

        if (!_state.TryGetAtIndex(index, out ReadOnlyMemory<T> field))
        {
            Throw.Argument_FieldIndex(index, _state);
        }

        if (!_options.GetConverter<TValue>().TryParse(field.Span, out value))
        {
            value = default;
            return false;
        }

        return true;
    }

    public bool TryGetValue<TValue>(string name, [MaybeNullWhen(false)] out TValue value)
    {
        _state.EnsureVersion(_version);

        if (!_state.TryGetHeaderIndex(name, out int index))
        {
            Throw.Argument_HeaderNameNotFound(name, _options.AllowContentInExceptions, _state.Header.Keys);
        }

        return TryGetValue(index, out value);
    }

    public TValue GetField<TValue>(string name)
    {
        _state.EnsureVersion(_version);

        if (!_state.TryGetHeaderIndex(name, out int index))
        {
            Throw.Argument_HeaderNameNotFound(name, _options.AllowContentInExceptions, _state.Header.Keys);
        }

        return GetField<TValue>(index);
    }

    /// <inheritdoc cref="ICsvRecord{T}.GetField{TValue}(int)"/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public TValue GetField<TValue>(int index)
    {
        _state.EnsureVersion(_version);

        if (!_state.TryGetAtIndex(index, out ReadOnlySpan<T> field))
        {
            Throw.Argument_FieldIndex(index, _state);
        }

        var converter = _options.GetConverter<TValue>();

        if (!converter.TryParse(field, out var value))
        {
            Throw.ParseFailed(field, converter, _options, typeof(TValue));
        }

        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetHeader() => _state.Header = null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(_version, _state);

    IEnumerator<ReadOnlyMemory<T>> IEnumerable<ReadOnlyMemory<T>>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public List<ReadOnlyMemory<T>> ToList()
    {
        List<ReadOnlyMemory<T>> list = [];

        foreach (ReadOnlyMemory<T> field in this)
        {
            list.Add(field.SafeCopy());
        }

        return list;
    }

    [RUF(Messages.CompiledExpressions), RDC(Messages.CompiledExpressions)]
    public TRecord ParseRecord<[DAM(Messages.ReflectionBound)] TRecord>()
    {
        _state.EnsureVersion(_version);

        if (!_state.MaterializerCache.TryGetValue(typeof(TRecord), out object? obj))
        {
            var header = _state.Header;

            if (header is not null)
            {
                var bindings = _options.GetHeaderBinder().Bind<TRecord>(header.Values);
                obj = _options.CreateMaterializerFrom(bindings);
            }
            else
            {
                obj = _options.GetMaterializer<T, TRecord>();
            }

            _state.MaterializerCache[typeof(TRecord)] = obj;
        }

        BufferFieldReader<T> reader = _state.CreateFieldReader();

        try
        {
            return ((IMaterializer<T, TRecord>)obj).Parse(ref reader);
        }
        finally
        {
            reader.Dispose();
        }
    }

    public TRecord ParseRecord<TRecord>(CsvTypeMap<T, TRecord> typeMap)
    {
        ArgumentNullException.ThrowIfNull(typeMap);

        _state.EnsureVersion(_version);

        if (!_state.MaterializerCache.TryGetValue(typeMap, out object? obj))
        {
            obj = _state.Header is not null
                ? typeMap.BindMembers(_state.Header.Values, _options)
                : typeMap.BindMembers(_options);

            _state.MaterializerCache[typeMap] = obj;
        }

        BufferFieldReader<T> reader = _state.CreateFieldReader();

        try
        {
            return ((IMaterializer<T, TRecord>)obj).Parse(ref reader);
        }
        finally
        {
            reader.Dispose();
        }
    }

    public override string ToString()
    {
        return $"{{ CsvValueRecord [{_options.GetAsString(RawRecord.Span)}] }}";
    }

    public struct Enumerator : IEnumerator<ReadOnlyMemory<T>>
    {
        public ReadOnlyMemory<T> Current { get; private set; }

        private readonly int _version;
        private readonly EnumeratorState<T> _state;
        private int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(int version, EnumeratorState<T> state)
        {
            state.EnsureVersion(version);

            _version = version;
            _state = state;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            _state.EnsureVersion(_version);

            ref var fields = ref _state.GetFields();

            if (_index < fields.Length)
            {
                Current = fields[_index++];
                return true;
            }

            Current = default;
            return false;
        }

        bool IEnumerator.MoveNext() => MoveNext();
        void IEnumerator.Reset() => _index = 0;
        readonly void IDisposable.Dispose() { }
        readonly ReadOnlyMemory<T> IEnumerator<ReadOnlyMemory<T>>.Current => Current;
        readonly object IEnumerator.Current => Current;
    }

    private sealed class CsvRecordDebugView
    {
        private readonly CsvValueRecord<T> _record;

        public CsvRecordDebugView(CsvValueRecord<T> record) => _record = record;

        public int Line => _record.Line;
        public long Position => _record.Position;
        public string[] Headers => _record._state.Header?.Keys.ToArray() ?? [];
        public ReadOnlyMemory<T>[] Fields => [.. _record.ToList()];
        public string[] FieldValues => [.. Fields.Select(f => _record._options.GetAsString(f.Span))];
    }

    public static explicit operator CsvRecord<T>(in CsvValueRecord<T> record) => new(in record);
}
