using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;
using FlameCsv.Extensions;
using FlameCsv.Reading;
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
    /// <inheritdoc/>
    public long Position { get; }

    /// <inheritdoc/>
    public int Line { get; }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public ReadOnlyMemory<T> this[int index] => GetField(index);

    /// <inheritdoc/>
    public ReadOnlyMemory<T> this[string name] => GetField(name);

    internal readonly EnumeratorState<T> _state;
    internal readonly CsvOptions<T> _options;

    private readonly int _version;
    private readonly ReadOnlyMemory<T> _record;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvValueRecord(
        long position,
        int lineIndex,
        ref readonly CsvLine<T> line,
        CsvOptions<T> options,
        EnumeratorState<T> state)
    {
        Position = position;
        Line = lineIndex;
        _record = line.Record;
        _options = options;
        _state = state;
        _version = _state.Initialize(in line);
    }

    /// <inheritdoc cref="ICsvRecord{T}.GetField(string)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyMemory<T> GetField(string name)
    {
        _state.EnsureVersion(_version);

        if (!_state.TryGetHeaderIndex(name, out int index))
        {
            Throw.Argument_HeaderNameNotFound(name, _state.Header.HeaderNames);
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public bool TryGetValue<TValue>(string name, [MaybeNullWhen(false)] out TValue value)
    {
        _state.EnsureVersion(_version);

        if (!_state.TryGetHeaderIndex(name, out int index))
        {
            Throw.Argument_HeaderNameNotFound(name, _state.Header.HeaderNames);
        }

        return TryGetValue(index, out value);
    }

    /// <inheritdoc/>
    public TValue GetField<TValue>(string name)
    {
        _state.EnsureVersion(_version);

        if (!_state.TryGetHeaderIndex(name, out int index))
        {
            Throw.Argument_HeaderNameNotFound(name, _state.Header.HeaderNames);
        }

        return GetField<TValue>(index);
    }

    /// <inheritdoc/>
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
            Throw.ParseFailed(field, converter, typeof(TValue));
        }

        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ResetHeader() => _state.Header = null;

    /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(_version, _state);

    IEnumerator<ReadOnlyMemory<T>> IEnumerable<ReadOnlyMemory<T>>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    public TRecord ParseRecord<[DAM(Messages.ReflectionBound)] TRecord>()
    {
        _state.EnsureVersion(_version);

        if (!_state.MaterializerCache.TryGetValue(typeof(TRecord), out object? obj))
        {
            var header = _state.Header;

            obj = header is not null
                ? _options.TypeBinder.GetMaterializer<TRecord>(header.Values)
                : _options.TypeBinder.GetMaterializer<TRecord>();

            _state.MaterializerCache[typeof(TRecord)] = obj;
        }

        BufferFieldReader<T> reader = _state.CreateFieldReader();
        return ((IMaterializer<T, TRecord>)obj).Parse(ref reader);
    }

    /// <inheritdoc/>
    public TRecord ParseRecord<TRecord>(CsvTypeMap<T, TRecord> typeMap)
    {
        ArgumentNullException.ThrowIfNull(typeMap);

        _state.EnsureVersion(_version);

        if (!_state.MaterializerCache.TryGetValue(typeMap, out object? obj))
        {
            obj = _state.Header is not null
                ? typeMap.GetMaterializer(_state.Header.Values, _options)
                : typeMap.GetMaterializer(_options);

            _state.MaterializerCache[typeMap] = obj;
        }

        BufferFieldReader<T> reader = _state.CreateFieldReader();
        return ((IMaterializer<T, TRecord>)obj).Parse(ref reader);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"{{ CsvValueRecord [{_options.GetAsString(RawRecord.Span)}] }}";
    }

    /// <summary>
    /// Enumerates the fields in the record.
    /// </summary>
    public struct Enumerator : IEnumerator<ReadOnlyMemory<T>>
    {
        /// <summary>
        /// Current field in the record.
        /// </summary>
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

        /// <inheritdoc cref="IEnumerator.MoveNext"/>
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

    private sealed class CsvRecordDebugView(CsvValueRecord<T> record)
    {
        private readonly CsvValueRecord<T> _record = record;

        public int Line => _record.Line;
        public long Position => _record.Position;
        public string[] Headers => _record._state.Header?.Values.ToArray() ?? [];
        public ReadOnlyMemory<T>[] Fields => [.. _record];
        public string[] FieldValues => [.. Fields.Select(f => _record._options.GetAsString(f.Span))];
    }

    /// <summary>
    /// Preserves the data in the value record.
    /// </summary>
    public static explicit operator CsvRecord<T>(in CsvValueRecord<T> record) => new(in record);
}
