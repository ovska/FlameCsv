using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;
using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// Represents the current record when reading CSV.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
[PublicAPI]
[DebuggerTypeProxy(typeof(CsvRecord<>.CsvRecordDebugView))]
public readonly struct CsvRecord<T> : ICsvRecord<T> where T : unmanaged, IBinaryInteger<T>
{
    /// <inheritdoc cref="CsvPreservedRecord{T}.Position"/>
    public long Position { get; }

    /// <inheritdoc cref="CsvPreservedRecord{T}.Line"/>
    public int Line { get; }

    /// <inheritdoc cref="CsvPreservedRecord{T}.RawRecord"/>
    public ReadOnlySpan<T> RawRecord
    {
        get
        {
            _owner.EnsureVersion(_version);
            return _record.Record.Span;
        }
    }

    /// <inheritdoc cref="CsvPreservedRecord{T}.Header"/>
    public CsvHeader? Header
    {
        get
        {
            _owner.EnsureVersion(_version);
            return _owner.Header;
        }
    }

    /// <inheritdoc cref="CsvPreservedRecord{T}.Contains(CsvFieldIdentifier)"/>
    public bool Contains(CsvFieldIdentifier id)
    {
        _owner.EnsureVersion(_version);

        if (!id.TryGetIndex(out int index, out string? name))
        {
            CsvHeader? header = _owner.Header;

            if (header is null)
            {
                Throw.NotSupported_CsvHasNoHeader();
            }

            if (!header.TryGetValue(name, out index))
            {
                return false;
            }
        }

        return (uint)index < (uint)_record.FieldCount;
    }

    /// <inheritdoc cref="CsvPreservedRecord{T}.Options"/>
    public CsvOptions<T> Options => _options;

    /// <inheritdoc cref="CsvPreservedRecord{T}.this[CsvFieldIdentifier]"/>
    public ReadOnlySpan<T> this[CsvFieldIdentifier id] => GetField(id);

    ReadOnlySpan<T> ICsvRecord<T>.this[int index] => GetField(index);

    internal readonly IRecordOwner _owner;
    internal readonly CsvOptions<T> _options;
    internal readonly CsvFields<T> _record;
    private readonly int _version;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvRecord(
        int version,
        long position,
        int lineIndex,
        ref readonly CsvFields<T> record,
        CsvOptions<T> options,
        IRecordOwner owner
    )
    {
        _version = version;
        Position = position;
        Line = lineIndex;
        _record = record;
        _options = options;
        _owner = owner;
    }

    /// <inheritdoc cref="CsvPreservedRecord{T}.GetField(CsvFieldIdentifier)"/>
    private ReadOnlySpan<T> GetField(CsvFieldIdentifier id, out int index)
    {
        _owner.EnsureVersion(_version);

        if (!id.TryGetIndex(out index, out string? name))
        {
            if (_owner.Header is null)
            {
                Throw.NotSupported_CsvHasNoHeader();
            }

            if (!_owner.Header.TryGetValue(name, out index))
            {
                Throw.Argument_HeaderNameNotFound(name, _owner.Header.Values);
            }
        }

        if ((uint)index >= (uint)_record.FieldCount)
        {
            Throw.Argument_FieldIndex(index, _record.FieldCount, id.UnsafeName);
        }

        return _record.GetField(index);
    }

    /// <inheritdoc cref="CsvPreservedRecord{T}.GetField(CsvFieldIdentifier)"/>
    public ReadOnlySpan<T> GetField(CsvFieldIdentifier id) => GetField(id, out _);

    /// <inheritdoc cref="CsvPreservedRecord{T}.FieldCount"/>
    public int FieldCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            _owner.EnsureVersion(_version);
            return _record.FieldCount;
        }
    }

    /// <inheritdoc cref="CsvPreservedRecord{T}.TryParseField{TValue}(CsvFieldIdentifier,out TValue)"/>
    [RUF(Messages.ConverterOverload), RDC(Messages.ConverterOverload)]
    public bool TryParseField<TValue>(CsvFieldIdentifier id, [MaybeNullWhen(false)] out TValue value)
    {
        var field = GetField(id);
        return _options.GetConverter<TValue>().TryParse(field, out value);
    }

    /// <inheritdoc cref="CsvPreservedRecord{T}.TryParseField{TValue}(CsvConverter{T,TValue},CsvFieldIdentifier,out TValue)"/>
    public bool TryParseField<TValue>(
        CsvConverter<T, TValue> converter,
        CsvFieldIdentifier id,
        [MaybeNullWhen(false)] out TValue value
    )
    {
        ArgumentNullException.ThrowIfNull(converter);
        var field = GetField(id);
        return converter.TryParse(field, out value);
    }

    /// <inheritdoc cref="CsvPreservedRecord{T}.ParseField{TValue}(CsvFieldIdentifier)"/>
    [RUF(Messages.ConverterOverload), RDC(Messages.ConverterOverload)]
    public TValue ParseField<TValue>(CsvFieldIdentifier id)
    {
        var field = GetField(id, out int fieldIndex);

        var converter = _options.GetConverter<TValue>();

        if (!converter.TryParse(field, out var value))
        {
            ThrowParseException(typeof(TValue), fieldIndex, id, converter);
        }

        return value;
    }

    /// <inheritdoc cref="CsvPreservedRecord{T}.ParseField{TValue}(CsvConverter{T,TValue},CsvFieldIdentifier)"/>
    public TValue ParseField<TValue>(CsvConverter<T, TValue> converter, CsvFieldIdentifier id)
    {
        ArgumentNullException.ThrowIfNull(converter);

        var field = GetField(id, out int fieldIndex);

        if (!converter.TryParse(field, out var value))
        {
            ThrowParseException(typeof(TValue), fieldIndex, id, converter);
        }

        return value;
    }

    /// <summary>
    /// Returns an enumerator that can be used to read the fields one by one.
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(_version, _owner, in _record);

    /// <inheritdoc cref="CsvPreservedRecord{T}.ParseRecord{TRecord>()"/>
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    public TRecord ParseRecord<[DAM(Messages.ReflectionBound)] TRecord>()
    {
        _owner.EnsureVersion(_version);

        // read to local as hot reload can reset the cache
        IDictionary<object, object> cache = _owner.MaterializerCache;

        if (!cache.TryGetValue(typeof(TRecord), out object? obj))
        {
            var header = _owner.Header;

            obj = header is not null
                ? _options.TypeBinder.GetMaterializer<TRecord>(header.Values)
                : _options.TypeBinder.GetMaterializer<TRecord>();

            cache[typeof(TRecord)] = obj;
        }

        var materializer = (IMaterializer<T, TRecord>)obj;
        CsvRecordRef<T> reader = new(in _record);
        return materializer.Parse(ref reader);
    }

    /// <inheritdoc cref="CsvPreservedRecord{T}.ParseRecord{TRecord}(CsvTypeMap{T,TRecord})"/>
    public TRecord ParseRecord<TRecord>(CsvTypeMap<T, TRecord> typeMap)
    {
        ArgumentNullException.ThrowIfNull(typeMap);

        _owner.EnsureVersion(_version);

        // read to local as hot reload can reset the cache
        IDictionary<object, object> cache = _owner.MaterializerCache;

        if (!cache.TryGetValue(typeMap, out object? obj))
        {
            obj = _owner.Header is not null
                ? typeMap.GetMaterializer(_owner.Header.Values, _options)
                : typeMap.GetMaterializer(_options);

            cache[typeMap] = obj;
        }

        var materializer = (IMaterializer<T, TRecord>)obj;
        CsvRecordRef<T> reader = new(in _record);
        return materializer.Parse(ref reader);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"{{ CsvValueRecord[{_record.FieldCount}] \"{_options.GetAsString(RawRecord)}\" }}";
    }

    /// <summary>
    /// Ensures the struct is not <c>default</c> and the version is valid.
    /// </summary>
    internal void EnsureValid()
    {
        Throw.IfDefaultStruct(_owner is null, typeof(CsvRecord<T>));
        _owner.EnsureVersion(_version);
    }

    internal int GetFieldIndex(CsvFieldIdentifier id, [CallerArgumentExpression(nameof(id))] string paramName = "")
    {
        if (!id.TryGetIndex(out int index, out string? name))
        {
            if (_owner.Header is null)
            {
                Throw.NotSupported_CsvHasNoHeader();
            }

            if (!_owner.Header.TryGetValue(name, out index))
            {
                Throw.Argument_HeaderNameNotFound(name, _owner.Header.Values);
            }
        }

        if ((uint)index >= (uint)_record.FieldCount)
        {
            Throw.Argument_FieldIndex(index, _record.FieldCount, paramName);
        }

        return index;
    }

    /// <summary>
    /// Enumerates the fields in the record.
    /// </summary>
    public struct Enumerator
    {
        /// <summary>
        /// Current field in the record.
        /// </summary>
        public readonly ReadOnlySpan<T> Current => _record.GetField(_index - 1);

        private readonly int _version;
        private readonly IRecordOwner _owner;
        private readonly CsvFields<T> _record;
        private int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(int version, IRecordOwner owner, scoped ref readonly CsvFields<T> record)
        {
            owner.EnsureVersion(version);

            _version = version;
            _owner = owner;
            _record = record;
        }

        /// <inheritdoc cref="IEnumerator.MoveNext"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            _owner.EnsureVersion(_version);

            if (_index < _record.FieldCount)
            {
                _index++;
                return true;
            }

            _index = int.MaxValue;
            return false;
        }
    }

    private sealed class CsvRecordDebugView(CsvRecord<T> record)
    {
        private readonly CsvRecord<T> _record = record;

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
    public CsvPreservedRecord<T> Preserve() => new(in this);

    /// <inheritdoc cref="Preserve"/>
    public static explicit operator CsvPreservedRecord<T>(in CsvRecord<T> record) => new(in record);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowParseException(Type type, int index, CsvFieldIdentifier id, object converter)
    {
        string target = id.ToString();

        var ex = new CsvParseException($"Failed to parse {type.Name} {target} using {converter.GetType().Name}.")
        {
            Converter = converter, FieldIndex = index, Target = target,
        };

        ex.Enrich(Line, Position, in _record);
        throw ex;
    }
}
