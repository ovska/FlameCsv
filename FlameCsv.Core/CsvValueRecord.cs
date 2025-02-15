using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;
using FlameCsv.Enumeration;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Utilities;
using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// Represents the current record when reading CSV.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
[PublicAPI]
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
            _owner.EnsureVersion(_version);
            return _record;
        }
    }

    /// <summary>
    /// Whether the current CSV enumeration has a header.
    /// </summary>
    public bool HasHeader => _owner.Header is not null;

    /// <inheritdoc/>
    public ReadOnlySpan<string> Header
    {
        get
        {
            _owner.EnsureVersion(_version);

            if (!_owner._hasHeader)
                Throw.NotSupported_CsvHasNoHeader();

            if (_owner.Header is null)
                Throw.InvalidOperation_HeaderNotRead();

            return _owner.Header.Values;
        }
    }

    /// <inheritdoc/>
    public bool Contains(CsvFieldIdentifier id)
    {
        return id.TryGetIndex(out int index, out string? name)
            ? (uint)index < (uint)_owner._fields.Length
            : _owner.Header?.ContainsKey(name) ?? false;
    }

    /// <inheritdoc/>
    public CsvOptions<T> Options => _options;

    /// <inheritdoc/>
    public ReadOnlyMemory<T> this[CsvFieldIdentifier id] => GetField(id);

    internal readonly CsvRecordEnumeratorBase<T> _owner;
    internal readonly CsvOptions<T> _options;

    private readonly int _version;
    private readonly ReadOnlyMemory<T> _record;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvValueRecord(
        int version,
        long position,
        int lineIndex,
        ref readonly CsvLine<T> line,
        CsvOptions<T> options,
        CsvRecordEnumeratorBase<T> owner)
    {
        Position = position;
        Line = lineIndex;
        _record = line.Record;
        _options = options;
        _owner = owner;
        _version = version;
    }

    /// <inheritdoc cref="ICsvRecord{T}.GetField(CsvFieldIdentifier)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyMemory<T> GetField(CsvFieldIdentifier id)
    {
        _owner.EnsureVersion(_version);

        if (!id.TryGetIndex(out int index, out string? name) && !_owner.TryGetHeaderIndex(name, out index))
        {
            Throw.Argument_HeaderNameNotFound(name, _owner.Header.HeaderNames);
        }

        if (!_owner.TryGetAtIndex(index, out ReadOnlyMemory<T> field))
        {
            Throw.Argument_FieldIndex(index, _owner._fields.Length, id.UnsafeName);
        }

        return field;
    }

    /// <inheritdoc cref="ICsvRecord{T}.FieldCount"/>
    public int FieldCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            _owner.EnsureVersion(_version);
            return _owner._fields.Length;
        }
    }

    /// <inheritdoc/>
    [RUF(Messages.ConverterOverload), RDC(Messages.ConverterOverload)]
    public bool TryParseField<TValue>(CsvFieldIdentifier id, [MaybeNullWhen(false)] out TValue value)
    {
        var field = GetField(id).Span;
        return _options.GetConverter<TValue>().TryParse(field, out value);
    }

    /// <inheritdoc/>
    public bool TryParseField<TValue>(
        CsvConverter<T, TValue> converter,
        CsvFieldIdentifier id,
        [MaybeNullWhen(false)] out TValue value)
    {
        ArgumentNullException.ThrowIfNull(converter);
        return converter.TryParse(GetField(id).Span, out value);
    }

    /// <inheritdoc/>
    [RUF(Messages.ConverterOverload), RDC(Messages.ConverterOverload)]
    public TValue ParseField<TValue>(CsvFieldIdentifier id)
    {
        var field = GetField(id).Span;

        var converter = _options.GetConverter<TValue>();

        if (!_options.GetConverter<TValue>().TryParse(field, out var value))
        {
            Throw.ParseFailed(field, converter, typeof(TValue));
        }

        return value;
    }

    /// <inheritdoc/>
    public TValue ParseField<TValue>(CsvConverter<T, TValue> converter, CsvFieldIdentifier id)
    {
        ArgumentNullException.ThrowIfNull(converter);

        var field = GetField(id).Span;

        if (!converter.TryParse(field, out var value))
        {
            Throw.ParseFailed(field, converter, typeof(TValue));
        }

        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ResetHeader() => _owner.Header = null;

    /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(_version, _owner);

    IEnumerator<ReadOnlyMemory<T>> IEnumerable<ReadOnlyMemory<T>>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    public TRecord ParseRecord<[DAM(Messages.ReflectionBound)] TRecord>()
    {
        _owner.EnsureVersion(_version);

        // read to local as hot reload can reset the cache
        Dictionary<object, object> cache = _owner.MaterializerCache;

        if (!cache.TryGetValue(typeof(TRecord), out object? obj))
        {
            var header = _owner.Header;

            obj = header is not null
                ? _options.TypeBinder.GetMaterializer<TRecord>(header.Values)
                : _options.TypeBinder.GetMaterializer<TRecord>();

            cache[typeof(TRecord)] = obj;
        }

        BufferFieldReader<T> reader = _owner._fields.CreateReader(_options, _record);
        return ((IMaterializer<T, TRecord>)obj).Parse(ref reader);
    }

    /// <inheritdoc/>
    public TRecord ParseRecord<TRecord>(CsvTypeMap<T, TRecord> typeMap)
    {
        ArgumentNullException.ThrowIfNull(typeMap);

        _owner.EnsureVersion(_version);

        // read to local as hot reload can reset the cache
        Dictionary<object, object> cache = _owner.MaterializerCache;

        if (!cache.TryGetValue(typeMap, out object? obj))
        {
            obj = _owner.Header is not null
                ? typeMap.GetMaterializer(_owner.Header.Values, _options)
                : typeMap.GetMaterializer(_options);

            cache[typeMap] = obj;
        }

        BufferFieldReader<T> reader = _owner._fields.CreateReader(_options, _record);
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
        private readonly CsvRecordEnumeratorBase<T> _state;
        private int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(int version, CsvRecordEnumeratorBase<T> state)
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

            if (_index < _state._fields.Length)
            {
                Current = _state._fields[_index++];
                return true;
            }

            Current = default;
            return false;
        }

        bool IEnumerator.MoveNext() => MoveNext();
        void IEnumerator.Reset()
        {
            _state.EnsureVersion(_version);
            _index = 0;
        }

        readonly void IDisposable.Dispose()
        {
        }

        readonly object IEnumerator.Current => Current;
    }

    private sealed class CsvRecordDebugView(CsvValueRecord<T> record)
    {
        private readonly CsvValueRecord<T> _record = record;

        public int Line => _record.Line;
        public long Position => _record.Position;
        public string[] Headers => _record._owner.Header?.Values.ToArray() ?? [];
        public ReadOnlyMemory<T>[] Fields => [.. _record];
        public string[] FieldValues => [.. Fields.Select(f => _record._options.GetAsString(f.Span))];
    }

    /// <summary>
    /// Preserves the data in the value record.
    /// </summary>
    public CsvRecord<T> Preserve() => new(in this);

    /// <inheritdoc cref="Preserve"/>
    public static explicit operator CsvRecord<T>(in CsvValueRecord<T> record) => new(in record);
}
