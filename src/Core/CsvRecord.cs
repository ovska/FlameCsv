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
public readonly partial struct CsvRecord<T> : ICsvRecord<T>, IEnumerable<ReadOnlySpan<T>>
    where T : unmanaged, IBinaryInteger<T>
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
            return _slice.RawValue;
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
            return _owner.Header?.ContainsKey(name) ?? false;
        }

        return (uint)index < (uint)_slice.FieldCount;
    }

    /// <inheritdoc cref="CsvPreservedRecord{T}.Options"/>
    public CsvOptions<T> Options => _slice.Reader.Options;

    /// <inheritdoc cref="CsvPreservedRecord{T}.this[CsvFieldIdentifier]"/>
    public ReadOnlySpan<T> this[CsvFieldIdentifier id] => GetField(id);

    ReadOnlySpan<T> ICsvRecord<T>.this[int index] => GetField(index, out _);

    internal readonly IRecordOwner _owner;
    internal readonly CsvSlice<T> _slice;
    private readonly int _version;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvRecord(int version, long position, int lineIndex, CsvSlice<T> slice, IRecordOwner owner)
    {
        _version = version;
        Position = position;
        Line = lineIndex;
        _slice = slice;
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

        if ((uint)index >= (uint)_slice.FieldCount)
        {
            Throw.Argument_FieldIndex(index, _slice.FieldCount, id.UnsafeName);
        }

        return _slice.GetField(index);
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
            return _slice.FieldCount;
        }
    }

    /// <inheritdoc cref="CsvPreservedRecord{T}.TryParseField{TValue}(CsvFieldIdentifier,out TValue)"/>
    [RUF(Messages.ConverterOverload), RDC(Messages.ConverterOverload)]
    public bool TryParseField<TValue>(CsvFieldIdentifier id, [MaybeNullWhen(false)] out TValue value)
    {
        var field = GetField(id);
        return Options.GetConverter<TValue>().TryParse(field, out value);
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

        var converter = Options.GetConverter<TValue>();

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
    public Enumerator GetEnumerator() => new(_version, _owner, in _slice);

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
                ? Options.TypeBinder.GetMaterializer<TRecord>(header.Values)
                : Options.TypeBinder.GetMaterializer<TRecord>();

            cache[typeof(TRecord)] = obj;
        }

        var materializer = (IMaterializer<T, TRecord>)obj;
        CsvRecordRef<T> record = new(in _slice);
        return materializer.Parse(ref record);
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
                ? typeMap.GetMaterializer(_owner.Header.Values, Options)
                : typeMap.GetMaterializer(Options);

            cache[typeMap] = obj;
        }

        var materializer = (IMaterializer<T, TRecord>)obj;
        CsvRecordRef<T> record = new(in _slice);
        return materializer.Parse(ref record);
    }

    IEnumerator<ReadOnlySpan<T>> IEnumerable<ReadOnlySpan<T>>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    [ExcludeFromCodeCoverage]
    public override string ToString()
    {
        return $"{{ CsvRecord[{_slice.FieldCount}] \"{Options.GetAsString(RawRecord)}\" }}";
    }

    /// <summary>
    /// Copies the fields in the record to a new array.
    /// </summary>
    /// <returns></returns>
    public string[] ToArray()
    {
        _owner.EnsureVersion(_version);

        var fields = new string[_slice.FieldCount];
        for (int i = 0; i < fields.Length; i++)
        {
            fields[i] = Options.GetAsString(_slice.GetField(i));
        }

        return fields;
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

        if ((uint)index >= (uint)_slice.FieldCount)
        {
            Throw.Argument_FieldIndex(index, _slice.FieldCount, paramName);
        }

        return index;
    }

    /// <summary>
    /// Enumerates the fields in the record.
    /// </summary>
    public struct Enumerator : IEnumerator<ReadOnlySpan<T>>
    {
        /// <summary>
        /// Current field in the record.
        /// </summary>
        public readonly ReadOnlySpan<T> Current => _slice.GetField(_index - 1);

        private readonly int _version;
        private readonly IRecordOwner _owner;
        private readonly CsvSlice<T> _slice;
        private int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(int version, IRecordOwner owner, scoped ref readonly CsvSlice<T> slice)
        {
            owner.EnsureVersion(version);

            _version = version;
            _owner = owner;
            _slice = slice;
        }

        /// <inheritdoc cref="IEnumerator.MoveNext"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            _owner.EnsureVersion(_version);

            if (_index < _slice.FieldCount)
            {
                _index++;
                return true;
            }

            _index = int.MaxValue;
            return false;
        }

        void IDisposable.Dispose() { }

        void IEnumerator.Reset()
        {
            _index = 0;
        }

        object IEnumerator.Current
        {
            get { throw new NotSupportedException(); }
        }
    }

    [ExcludeFromCodeCoverage]
    private sealed class CsvRecordDebugView(CsvRecord<T> record)
    {
        private readonly CsvRecord<T> _record = record;

        public int Line => _record.Line;
        public long Position => _record.Position;
        public string[] Headers => _record._owner.Header?.Values.ToArray() ?? [];

        public string[] Fields
        {
            get
            {
                if (_fields is null)
                {
                    var fields = new string[_record.FieldCount];

                    for (int i = 0; i < fields.Length; i++)
                    {
                        fields[i] = _record.Options.GetAsString(_record.GetField(i));
                    }

                    _fields = fields;
                }

                return _fields;
            }
        }

        private string[]? _fields;
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
            Converter = converter,
            FieldIndex = index,
            Target = target,
        };

        ex.Enrich(Line, Position, in _slice);
        throw ex;
    }
}
