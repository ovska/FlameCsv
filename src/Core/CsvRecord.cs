using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Binding;
using FlameCsv.Enumeration;
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
public readonly partial struct CsvRecord<T> : IEnumerable<ReadOnlySpan<T>>
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Start position of the record in the original data.
    /// </summary>
    public long Position { get; }

    /// <summary>
    /// 1-based line number in the CSV data. Empty lines and the header are counted.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// Raw data of the record as a single memory block, not including a possible trailing newline.
    /// </summary>
    public ReadOnlySpan<T> Raw
    {
        get
        {
            _owner.EnsureVersion(_version);

            ReadOnlySpan<uint> fields = FieldsWithPrevious;
            int end = Field.End(fields[^1]);
            int start = Field.NextStart(fields[0]);
            return _owner.Reader._buffer.Span[start..end];
        }
    }

    /// <summary>
    /// Returns the header record for the current CSV, or null if <see cref="CsvOptions{T}.HasHeader"/> is <c>false</c>.
    /// </summary>
    public CsvHeader? Header
    {
        get
        {
            _owner.EnsureVersion(_version);
            return _owner.Header;
        }
    }

    /// <summary>
    /// Returns <c>true</c> if the record contains the specified field.
    /// </summary>
    public bool Contains(CsvFieldIdentifier id)
    {
        _owner.EnsureVersion(_version);

        if (!id.TryGetIndex(out int index, out string? name))
        {
            return _owner.Header?.ContainsKey(name) ?? false;
        }

        return (uint)index < (uint)_view.Length;
    }

    /// <summary>
    /// The options-instance associated with the current CSV.
    /// </summary>
    public CsvOptions<T> Options => _owner.Options;

    /// <inheritdoc cref="GetField(CsvFieldIdentifier)"/>
    public ReadOnlySpan<T> this[CsvFieldIdentifier id] => GetField(id);

    internal readonly CsvRecordEnumerator<T> _owner;
    internal readonly RecordView _view;
    private readonly int _version;

    internal ReadOnlySpan<uint> FieldsWithPrevious
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _owner.Reader._recordBuffer._fields.AsSpan(_view.Start, _view.Length + 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvRecord(int version, long position, int lineIndex, RecordView view, CsvRecordEnumerator<T> owner)
    {
        _version = version;
        Position = position;
        Line = lineIndex;
        _view = view;
        _owner = owner;
    }

    /// <summary>
    /// Converts to a <see cref="CsvRecordRef{T}"/>.
    /// </summary>
    public static explicit operator CsvRecordRef<T>(in CsvRecord<T> record)
    {
        record.EnsureValid();
        return new CsvRecordRef<T>(record._owner.Reader, record._view);
    }

    private ReadOnlySpan<T> GetField(CsvFieldIdentifier id, out int index)
    {
        _owner.EnsureVersion(_version);

        if (!id.TryGetIndex(out index, out string? name))
        {
            CsvHeader? header = _owner.Header;

            if (header is null)
            {
                Throw.NotSupported_CsvHasNoHeader();
            }

            if (!header.TryGetValue(name, out index))
            {
                Throw.Argument_HeaderNameNotFound(name, header.Values);
            }
        }
        else if ((uint)index >= (uint)_view.Length)
        {
            Throw.Argument_FieldIndex(index, _view.Length, id.UnsafeName);
        }

        CsvReader<T> reader = _owner.Reader;
        ReadOnlySpan<T> dataSpan = reader._buffer.Span;
        ReadOnlySpan<uint> fields = FieldsWithPrevious;

        uint previous = fields[index];
        uint current = fields[index + 1];

        int start = Field.NextStart(previous);
        int end = Field.End(current);

        Check.GreaterThanOrEqual(end, start, "Malformed field");

        if (reader._dialect.Trimming == 0 && (int)(current << 2) >= 0)
        {
            return dataSpan[start..end];
        }

        return Field.GetValue(start, current, ref MemoryMarshal.GetReference(dataSpan), _owner.Reader);
    }

    /// <summary>
    /// Returns the value of the field at the specified index.
    /// </summary>
    /// <param name="id">Field index of name</param>
    /// <returns>Field value, unescaped and stripped of quotes when applicable</returns>
    /// <exception cref="ArgumentException">The ID points to a field that does not exist</exception>
    /// <exception cref="NotSupportedException"><paramref name="id"/> points to a header name, but the CSV has no header</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> GetField(CsvFieldIdentifier id) => GetField(id, out _);

    /// <summary>
    /// Returns the number of fields in the record; always at least 1 in a valid record, even if the record's length is 0.
    /// Returns 0 if the struct is <c>default</c>.
    /// </summary>
    public int FieldCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _view.Length;
    }

    /// <summary>
    /// Attempts to parse a <typeparamref name="TValue"/> from a specific field.
    /// </summary>
    /// <typeparam name="TValue">Value parsed</typeparam>
    /// <param name="id">Field index of name</param>
    /// <param name="value">Parsed value, if successful</param>
    /// <returns><c>true</c> if the value was successfully parsed</returns>
    /// <exception cref="ArgumentException">The ID points to a field that does not exist</exception>
    /// <exception cref="CsvConverterMissingException">Converter not found for <typeparamref name="TValue"/></exception>
    [RUF(Messages.ConverterOverload), RDC(Messages.ConverterOverload)]
    public bool TryParseField<TValue>(CsvFieldIdentifier id, [MaybeNullWhen(false)] out TValue value)
    {
        var field = GetField(id);
        return Options.GetConverter<TValue>().TryParse(field, out value);
    }

    /// <summary>
    /// Attempts to parse a <typeparamref name="TValue"/> from a specific field.
    /// </summary>
    /// <typeparam name="TValue">Value parsed</typeparam>
    /// <param name="converter">Converter to parse the field with</param>
    /// <param name="id">Field index of name</param>
    /// <param name="value">Parsed value, if successful</param>
    /// <returns><c>true</c> if the value was successfully parsed</returns>
    /// <exception cref="ArgumentException">The ID points to a field that does not exist</exception>
    /// <exception cref="CsvConverterMissingException">Converter not found for <typeparamref name="TValue"/></exception>
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

    /// <summary>
    /// Parses a value of type <typeparamref name="TValue"/> from a specific field.
    /// </summary>
    /// <typeparam name="TValue">Value parsed</typeparam>
    /// <param name="id">Field index of name</param>
    /// <returns>Parsed value</returns>
    /// <exception cref="ArgumentException">The ID points to a field that does not exist</exception>
    /// <exception cref="CsvConverterMissingException">Converter not found for <typeparamref name="TValue"/></exception>
    /// <exception cref="CsvParseException">The field value could not be parsed</exception>
    [RUF(Messages.ConverterOverload), RDC(Messages.ConverterOverload)]
    public TValue ParseField<TValue>(CsvFieldIdentifier id)
    {
        ReadOnlySpan<T> field = GetField(id, out int fieldIndex);

        var converter = Options.GetConverter<TValue>();

        if (!converter.TryParse(field, out var value))
        {
            ThrowParseException(typeof(TValue), fieldIndex, id, converter);
        }

        return value;
    }

    /// <summary>
    /// Parses a value of type <typeparamref name="TValue"/> from a specific field.
    /// </summary>
    /// <typeparam name="TValue">Value parsed</typeparam>
    /// <param name="converter">Converter to parse the field with</param>
    /// <param name="id">Field index of name</param>
    /// <returns>Parsed value</returns>
    /// <exception cref="ArgumentException">The ID points to a field that does not exist</exception>
    /// <exception cref="CsvConverterMissingException">Converter not found for <typeparamref name="TValue"/></exception>
    /// <exception cref="CsvParseException">The field value could not be parsed</exception>
    public TValue ParseField<TValue>(CsvConverter<T, TValue> converter, CsvFieldIdentifier id)
    {
        ArgumentNullException.ThrowIfNull(converter);

        ReadOnlySpan<T> field = GetField(id, out int fieldIndex);

        if (!converter.TryParse(field, out var value))
        {
            ThrowParseException(typeof(TValue), fieldIndex, id, converter);
        }

        return value;
    }

    /// <summary>
    /// Returns an enumerator that can be used to read the fields one by one.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator()
    {
        EnsureValid();
        return new(this);
    }

    /// <summary>
    /// Parses the record into an instance of <typeparamref name="TRecord"/> using reflection.
    /// </summary>
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
                ? Options.TypeBinder.GetMaterializer<TRecord>(header.Values)
                : Options.TypeBinder.GetMaterializer<TRecord>();

            cache[typeof(TRecord)] = obj;
        }

        var materializer = (IMaterializer<T, TRecord>)obj;
        return materializer.Parse((CsvRecordRef<T>)this);
    }

    /// <summary>
    /// Parses the record into an instance of <typeparamref name="TRecord"/> by using the type map.
    /// </summary>
    public TRecord ParseRecord<TRecord>(CsvTypeMap<T, TRecord> typeMap)
    {
        ArgumentNullException.ThrowIfNull(typeMap);

        _owner.EnsureVersion(_version);

        // read to local as hot reload can reset the cache
        Dictionary<object, object> cache = _owner.MaterializerCache;

        if (!cache.TryGetValue(typeMap, out object? obj))
        {
            obj = _owner.Header is not null
                ? typeMap.GetMaterializer(_owner.Header.Values, Options)
                : typeMap.GetMaterializer(Options);

            cache[typeMap] = obj;
        }

        var materializer = (IMaterializer<T, TRecord>)obj;
        return materializer.Parse((CsvRecordRef<T>)this);
    }

    IEnumerator<ReadOnlySpan<T>> IEnumerable<ReadOnlySpan<T>>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    [ExcludeFromCodeCoverage]
    public override string ToString()
    {
        return $"{{ CsvRecord[{_view.Length}] \"{Transcode.ToString(Raw)}\" }}";
    }

    /// <summary>
    /// Copies the fields in the record to a new array.
    /// </summary>
    public string[] ToArray()
    {
        _owner.EnsureVersion(_version);

        var fields = new string[_view.Length];
        for (int i = 0; i < fields.Length; i++)
        {
            fields[i] = Transcode.ToString(GetField(i, out _));
        }

        return fields;
    }

    /// <summary>
    /// Ensures the struct is not <c>default</c> and the version is valid.
    /// </summary>
    internal void EnsureValid()
    {
        if (_owner is null)
            Throw.InvalidOp_DefaultStruct(typeof(CsvRecord<T>));

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

        if ((uint)index >= (uint)_view.Length)
        {
            Throw.Argument_FieldIndex(index, _view.Length, paramName);
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
        public readonly ReadOnlySpan<T> Current => _record.GetField(_index - 1);

        private readonly CsvRecord<T> _record;
        private int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(CsvRecord<T> record)
        {
            _record = record;
        }

        /// <inheritdoc cref="IEnumerator.MoveNext"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_index < _record._view.Length)
            {
                _index++;
                return true;
            }

            _index = int.MaxValue;
            return false;
        }

        readonly void IDisposable.Dispose() { }

        void IEnumerator.Reset()
        {
            _index = 0;
        }

        readonly object IEnumerator.Current => throw new NotSupportedException("ReadOnlySpan cannot be boxed");
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
                        fields[i] = Transcode.ToString(_record.GetField(i));
                    }

                    _fields = fields;
                }

                return _fields;
            }
        }

        private string[]? _fields;
    }

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

        ex.Enrich((CsvRecordRef<T>)this);
        ex.HeaderValue = id.UnsafeName;
        throw ex;
    }

#if DEBUG
    [ExcludeFromCodeCoverage]
    static CsvRecord()
    {
        Check.LessThanOrEqual(Unsafe.SizeOf<CsvRecord<T>>(), 32);
    }
#endif
}
