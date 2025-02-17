using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;
using FlameCsv.Enumeration;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// Represents the current record when reading CSV.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
[PublicAPI]
[DebuggerTypeProxy(typeof(CsvValueRecord<>.CsvRecordDebugView))]
public readonly struct CsvValueRecord<T> : ICsvRecordFields<T>
    where T : unmanaged, IBinaryInteger<T>
{
    /// <inheritdoc cref="CsvRecord{T}.Position"/>
    public long Position { get; }

    /// <inheritdoc cref="CsvRecord{T}.Line"/>
    public int Line { get; }

    /// <inheritdoc cref="CsvRecord{T}.RawRecord"/>
    public ReadOnlySpan<T> RawRecord
    {
        get
        {
            _owner.EnsureVersion(_version);
            return _line.Record.Span;
        }
    }

    /// <inheritdoc cref="CsvRecord{T}.HasHeader"/>
    public bool HasHeader => _owner.Header is not null;

    /// <inheritdoc cref="CsvRecord{T}.Header"/>
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

    /// <inheritdoc cref="CsvRecord{T}.Contains(CsvFieldIdentifier)"/>
    public bool Contains(CsvFieldIdentifier id)
    {
        _owner.EnsureVersion(_version);

        return (id.TryGetIndex(out int index, out string? name) || _owner.TryGetHeaderIndex(name, out index)) &&
            (uint)index < (uint)_line.FieldCount;
    }

    /// <inheritdoc cref="CsvRecord{T}.Options"/>
    public CsvOptions<T> Options => _options;

    /// <inheritdoc cref="CsvRecord{T}.this[CsvFieldIdentifier]"/>
    public ReadOnlySpan<T> this[CsvFieldIdentifier id] => GetField(id);

    ReadOnlySpan<T> ICsvRecordFields<T>.this[int index] => GetField(index);

    internal readonly CsvRecordEnumerator<T> _owner;
    internal readonly CsvOptions<T> _options;
    internal readonly CsvLine<T> _line;
    private readonly int _version;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvValueRecord(
        int version,
        long position,
        int lineIndex,
        ref readonly CsvLine<T> line,
        CsvOptions<T> options,
        CsvRecordEnumerator<T> owner)
    {
        _version = version;
        Position = position;
        Line = lineIndex;
        _line = line;
        _options = options;
        _owner = owner;
    }

    /// <inheritdoc cref="CsvRecord{T}.GetField(CsvFieldIdentifier)"/>
    public ReadOnlySpan<T> GetField(CsvFieldIdentifier id)
    {
        _owner.EnsureVersion(_version);

        if (!id.TryGetIndex(out int index, out string? name) && !_owner.TryGetHeaderIndex(name, out index))
        {
            Throw.Argument_HeaderNameNotFound(name, _owner.Header.HeaderNames);
        }

        if ((uint)index >= (uint)_line.FieldCount)
        {
            Throw.Argument_FieldIndex(index, _line.FieldCount, id.UnsafeName);
        }

        return _line.GetField(index);
    }

    /// <inheritdoc cref="CsvRecord{T}.FieldCount"/>
    public int FieldCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            _owner.EnsureVersion(_version);
            return _line.FieldCount;
        }
    }

    /// <inheritdoc cref="CsvRecord{T}.TryParseField{TValue}(FlameCsv.CsvFieldIdentifier,out TValue)"/>
    [RUF(Messages.ConverterOverload), RDC(Messages.ConverterOverload)]
    public bool TryParseField<TValue>(CsvFieldIdentifier id, [MaybeNullWhen(false)] out TValue value)
    {
        var field = GetField(id);
        return _options.GetConverter<TValue>().TryParse(field, out value);
    }

    /// <inheritdoc cref="CsvRecord{T}.TryParseField{TValue}(FlameCsv.CsvConverter{T,TValue},FlameCsv.CsvFieldIdentifier,out TValue)"/>
    public bool TryParseField<TValue>(
        CsvConverter<T, TValue> converter,
        CsvFieldIdentifier id,
        [MaybeNullWhen(false)] out TValue value)
    {
        ArgumentNullException.ThrowIfNull(converter);
        var field = GetField(id);
        return converter.TryParse(field, out value);
    }

    /// <inheritdoc cref="CsvRecord{T}.ParseField{TValue}(FlameCsv.CsvFieldIdentifier)"/>
    [RUF(Messages.ConverterOverload), RDC(Messages.ConverterOverload)]
    public TValue ParseField<TValue>(CsvFieldIdentifier id)
    {
        var field = GetField(id);

        var converter = _options.GetConverter<TValue>();

        if (!_options.GetConverter<TValue>().TryParse(field, out var value))
        {
            Throw.ParseFailed(field, converter, typeof(TValue));
        }

        return value;
    }

    /// <inheritdoc cref="CsvRecord{T}.ParseField{TValue}(FlameCsv.CsvConverter{T,TValue},FlameCsv.CsvFieldIdentifier)"/>
    public TValue ParseField<TValue>(CsvConverter<T, TValue> converter, CsvFieldIdentifier id)
    {
        ArgumentNullException.ThrowIfNull(converter);

        var field = GetField(id);

        if (!converter.TryParse(field, out var value))
        {
            Throw.ParseFailed(field, converter, typeof(TValue));
        }

        return value;
    }

    /// <summary>
    /// Returns an enumerator that can be used to read the fields one by one.
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(_version, _owner, in _line);

    /// <inheritdoc cref="CsvRecord{T}.ParseRecord{TRecord>()"/>
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

        var materializer = (IMaterializer<T, TRecord>)obj;
        MetaFieldReader<T> reader = new(in _line, stackalloc T[Token<T>.StackLength]);
        return materializer.Parse(ref reader);
    }

    /// <inheritdoc cref="CsvRecord{T}.ParseRecord{TRecord}(FlameCsv.Binding.CsvTypeMap{T,TRecord})"/>
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

        var materializer = (IMaterializer<T, TRecord>)obj;
        MetaFieldReader<T> reader = new(in _line, stackalloc T[Token<T>.StackLength]);
        return materializer.Parse(ref reader);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"{{ CsvValueRecord [{_options.GetAsString(RawRecord)}] }}";
    }

    /// <summary>
    /// Enumerates the fields in the record.
    /// </summary>
    public struct Enumerator
    {
        /// <summary>
        /// Current field in the record.
        /// </summary>
        public ReadOnlySpan<T> Current => _line.GetField(_index - 1);

        private readonly int _version;
        private readonly CsvRecordEnumerator<T> _state;
        private readonly CsvLine<T> _line;
        private int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(int version, CsvRecordEnumerator<T> state, scoped ref readonly CsvLine<T> line)
        {
            state.EnsureVersion(version);

            _version = version;
            _state = state;
            _line = line;
        }

        /// <inheritdoc cref="IEnumerator.MoveNext"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            _state.EnsureVersion(_version);

            if (_index < _line.FieldCount)
            {
                _index++;
                return true;
            }

            _index = int.MaxValue;
            return false;
        }
    }

    private sealed class CsvRecordDebugView(CsvValueRecord<T> record)
    {
        private readonly CsvValueRecord<T> _record = record;

        public int Line => _record.Line;
        public long Position => _record.Position;
        public string[] Headers => _record._owner.Header?.Values.ToArray() ?? [];
        public ReadOnlyMemory<T>[] Fields
        {
            get
            {
                if (_fields is null)
                {
                    var fields = new ReadOnlyMemory<T>[_record.FieldCount];

                    for (int i = 0; i < fields.Length; i++)
                    {
                        fields[i] = _record.GetField(i).ToArray();
                    }

                    _fields = fields;
                }

                return _fields;
            }
        }

        private ReadOnlyMemory<T>[]? _fields;

        public string[] FieldValues => [.. Fields.Select(f => _record._options.GetAsString(f.Span))];
    }

    /// <summary>
    /// Preserves the data in the value record.
    /// </summary>
    public CsvRecord<T> Preserve() => new(in this);

    /// <inheritdoc cref="Preserve"/>
    public static explicit operator CsvRecord<T>(in CsvValueRecord<T> record) => new(in record);
}
