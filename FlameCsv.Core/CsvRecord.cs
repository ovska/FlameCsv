using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using FlameCsv.Binding;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Utilities;
using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// A self-contained copy of a single CSV record.
/// </summary>
[PublicAPI]
public class CsvRecord<T>
    : ICsvFields<T>,
        IReadOnlyList<ReadOnlyMemory<T>>,
        IReadOnlyDictionary<CsvFieldIdentifier, ReadOnlyMemory<T>>
    where T : unmanaged, IBinaryInteger<T>
{
    /// <inheritdoc cref="GetField(CsvFieldIdentifier)"/>
    public virtual ReadOnlyMemory<T> this[CsvFieldIdentifier id] => GetField(id);

    /// <summary>
    /// Start position of the record in the original data.
    /// </summary>
    public virtual long Position { get; }

    /// <summary>
    /// 1-based line number in the CSV data. Empty lines and the header are counted.
    /// </summary>
    public virtual int Line { get; }

    /// <summary>
    /// The options-instance associated with the current CSV.
    /// </summary>
    public CsvOptions<T> Options { get; }

    /// <summary>
    /// Raw data of the record as a single memory block, not including a possible trailing newline.
    /// </summary>
    public virtual ReadOnlyMemory<T> RawRecord { get; }

    /// <summary>
    /// Returns true if the header has been parsed from the CSV the record and <see cref="Header"/> is safe to use.
    /// </summary>
    /// <remarks>
    /// The header isn't returned as a separate record, so this property is always true if the options-instance
    /// is configured to have a header, and always false if not.
    /// </remarks>
    [MemberNotNullWhen(true, nameof(_header))]
    public bool HasHeader => _header is not null;

    /// <summary>
    /// Returns <see langword="true"/> if the record contains the specified field.
    /// </summary>
    /// <remarks>
    /// This method does not throw even if the identifier is for a header name, but the record's CSV has no header.
    /// </remarks>
    public bool Contains(CsvFieldIdentifier id)
    {
        return id.TryGetIndex(out int index, out string? name)
            ? (uint)index < (uint)_fields.Length
            : HasHeader && _header.ContainsKey(name);
    }

    /// <summary>
    /// Returns the header record for the current CSV. Throws if <see cref="HasHeader"/> is <see langword="false"/>.
    /// </summary>
    /// <seealso cref="HasHeader"/>
    /// <exception cref="NotSupportedException">Options is configured not to have a header</exception>
    public ImmutableArray<string> Header
    {
        get
        {
            if (!Options.HasHeader) Throw.NotSupported_CsvHasNoHeader();
            if (!HasHeader) Throw.InvalidOperation_HeaderNotRead();
            return _header.Values;
        }
    }

    private readonly ArraySegment<T>[] _fields;
    private readonly CsvHeader? _header;

    /// <summary>
    /// Initializes a new instance, copying the record's data.
    /// </summary>
    public CsvRecord(in CsvValueRecord<T> record)
    {
        Throw.IfDefaultStruct(record._options is null, typeof(CsvValueRecord<T>));

        // we don't need to validate field count here, as a non-default CsvValueRecord validates it on init
        Position = record.Position;
        Line = record.Line;
        Options = record._options;
        RawRecord = record._fields.Record.SafeCopy();
        _header = record._owner.Header;

        using WritableBuffer<T> buffer = new(Options.Allocator);
        foreach (var field in record) buffer.Push(field);
        _fields = buffer.Preserve();
    }

    int IReadOnlyCollection<ReadOnlyMemory<T>>.Count => FieldCount;

    ReadOnlyMemory<T> IReadOnlyList<ReadOnlyMemory<T>>.this[int index] => this[index];

    /// <summary>
    /// Parses the record into an instance of <typeparamref name="TRecord"/> using reflection.
    /// </summary>
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    public TRecord ParseRecord<[DAM(Messages.ReflectionBound)] TRecord>()
    {
        IMaterializer<T, TRecord> materializer = _header is not null
            ? Options.TypeBinder.GetMaterializer<TRecord>(_header.Values)
            : Options.TypeBinder.GetMaterializer<TRecord>();

        CsvRecord<T> @this = this;
        return materializer.Parse(ref @this);
    }

    /// <summary>
    /// Parses the record into an instance of <typeparamref name="TRecord"/> by using the type map.
    /// </summary>
    public TRecord ParseRecord<TRecord>(CsvTypeMap<T, TRecord> typeMap)
    {
        ArgumentNullException.ThrowIfNull(typeMap);

        IMaterializer<T, TRecord> materializer = _header is not null
            ? typeMap.GetMaterializer(_header.Values, Options)
            : typeMap.GetMaterializer(Options);

        CsvRecord<T> @this = this;
        return materializer.Parse(ref @this);
    }

    /// <summary>
    /// Returns the value of the field at the specified index.
    /// </summary>
    /// <param name="id">Field index of name</param>
    /// <returns>Field value, unescaped and stripped of quotes when applicable</returns>
    /// <exception cref="ArgumentException">The ID points to a field that does not exist</exception>
    /// <exception cref="NotSupportedException"><paramref name="id"/> points to a header name, but the CSV has no header</exception>
    public ReadOnlyMemory<T> GetField(CsvFieldIdentifier id) => GetField(id, out _);

    /// <inheritdoc cref="GetField(CsvFieldIdentifier)"/>
    protected virtual ReadOnlyMemory<T> GetField(CsvFieldIdentifier id, out int index)
    {
        if (!id.TryGetIndex(out index, out string? name))
        {
            if (!HasHeader || _header is null)
            {
                Throw.NotSupported_CsvHasNoHeader();
            }

            if (!_header.TryGetValue(name, out index))
            {
                Throw.Argument_HeaderNameNotFound(name, _header.Values.AsEnumerable());
            }
        }

        if ((uint)index >= _fields.Length)
        {
            Throw.Argument_FieldIndex(index, _fields.Length);
        }

        return _fields[index];
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
    public virtual TValue ParseField<TValue>(CsvConverter<T, TValue> converter, CsvFieldIdentifier id)
    {
        ArgumentNullException.ThrowIfNull(converter);

        var field = GetField(id, out int fieldIndex).Span;

        if (!converter.TryParse(field, out TValue? value))
        {
            CsvParseException.Throw(fieldIndex, typeof(TValue), converter, id.ToString());
        }

        return value;
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
    public virtual TValue ParseField<TValue>(CsvFieldIdentifier id)
    {
        var field = GetField(id, out int fieldIndex).Span;

        var converter = Options.GetConverter<TValue>();

        if (!converter.TryParse(field, out TValue? value))
        {
            CsvParseException.Throw(fieldIndex, typeof(TValue), converter, id.ToString());
        }

        return value;
    }

    /// <summary>
    /// Returns the number of fields in the record.
    /// </summary>
    public virtual int FieldCount => _fields.Length;

    ReadOnlySpan<T> ICsvFields<T>.this[int index] => _fields[index];

    /// <summary>
    /// Attempts to parse a <typeparamref name="TValue"/> from a specific field.
    /// </summary>
    /// <typeparam name="TValue">Value parsed</typeparam>
    /// <param name="id">Field index of name</param>
    /// <param name="value">Parsed value, if successful</param>
    /// <returns><see langword="true"/> if the value was successfully parsed</returns>
    /// <exception cref="ArgumentException">The ID points to a field that does not exist</exception>
    /// <exception cref="CsvConverterMissingException">Converter not found for <typeparamref name="TValue"/></exception>
    [RUF(Messages.ConverterOverload), RDC(Messages.ConverterOverload)]
    public virtual bool TryParseField<TValue>(CsvFieldIdentifier id, [MaybeNullWhen(false)] out TValue value)
    {
        var converter = Options.GetConverter<TValue>();
        return converter.TryParse(GetField(id).Span, out value);
    }

    /// <summary>
    /// Attempts to parse a <typeparamref name="TValue"/> from a specific field.
    /// </summary>
    /// <typeparam name="TValue">Value parsed</typeparam>
    /// <param name="converter">Converter to parse the field with</param>
    /// <param name="id">Field index of name</param>
    /// <param name="value">Parsed value, if successful</param>
    /// <returns><see langword="true"/> if the value was successfully parsed</returns>
    /// <exception cref="ArgumentException">The ID points to a field that does not exist</exception>
    /// <exception cref="CsvConverterMissingException">Converter not found for <typeparamref name="TValue"/></exception>
    public bool TryParseField<TValue>(
        CsvConverter<T, TValue> converter,
        CsvFieldIdentifier id,
        [MaybeNullWhen(false)] out TValue value)
    {
        ArgumentNullException.ThrowIfNull(converter);
        return converter.TryParse(GetField(id).Span, out value);
    }

    int IReadOnlyCollection<KeyValuePair<CsvFieldIdentifier, ReadOnlyMemory<T>>>.Count => _fields.Length;

    IEnumerator<KeyValuePair<CsvFieldIdentifier, ReadOnlyMemory<T>>>
        IEnumerable<KeyValuePair<CsvFieldIdentifier, ReadOnlyMemory<T>>>.GetEnumerator()
    {
        for (int i = 0; i < _fields.Length; i++)
        {
            yield return new KeyValuePair<CsvFieldIdentifier, ReadOnlyMemory<T>>(i, _fields[i]);
        }
    }

    IEnumerator<ReadOnlyMemory<T>> IEnumerable<ReadOnlyMemory<T>>.GetEnumerator()
    {
        for (int i = 0; i < _fields.Length; i++)
        {
            yield return _fields[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<ReadOnlyMemory<T>>)this).GetEnumerator();

    IEnumerable<CsvFieldIdentifier> IReadOnlyDictionary<CsvFieldIdentifier, ReadOnlyMemory<T>>.Keys
    {
        get
        {
            for (int i = 0; i < _fields.Length; i++) yield return i;
        }
    }

    IEnumerable<ReadOnlyMemory<T>> IReadOnlyDictionary<CsvFieldIdentifier, ReadOnlyMemory<T>>.Values
    {
        get
        {
            foreach (var f in _fields) yield return f;
        }
    }

    bool IReadOnlyDictionary<CsvFieldIdentifier, ReadOnlyMemory<T>>.ContainsKey(CsvFieldIdentifier key)
    {
        return Contains(key);
    }

    bool IReadOnlyDictionary<CsvFieldIdentifier, ReadOnlyMemory<T>>.TryGetValue(
        CsvFieldIdentifier key,
        out ReadOnlyMemory<T> value)
    {
        if (Contains(key))
        {
            value = GetField(key);
            return true;
        }

        value = default;
        return false;
    }
}
