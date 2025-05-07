using System.Collections;
using System.Diagnostics.CodeAnalysis;
using FlameCsv.Binding;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Utilities;
using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// A preserved copy of the data in a CSV record.
/// </summary>
[PublicAPI]
public class CsvPreservedRecord<T>
    : ICsvRecord<T>,
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
    /// Returns <c>true</c> if the record contains the specified field.
    /// </summary>
    /// <remarks>
    /// This method does not throw even if the identifier is for a header name, but the record's CSV has no header.
    /// </remarks>
    public bool Contains(CsvFieldIdentifier id)
    {
        return id.TryGetIndex(out int index, out string? name)
            ? (uint)index < (uint)_fields.Length
            : Header?.ContainsKey(name) ?? false;
    }

    /// <summary>
    /// Returns the header record for the current CSV, or null if <see cref="CsvOptions{T}.HasHeader"/> is <c>false</c>.
    /// </summary>
    public CsvHeader? Header { get; }

    internal readonly ArraySegment<T>[] _fields;

    /// <summary>
    /// Initializes a new instance, copying the record's data.
    /// </summary>
    public CsvPreservedRecord(in CsvRecord<T> record)
    {
        record.EnsureValid();

        // we don't need to validate field count here, as a non-default CsvValueRecord validates it on init
        Position = record.Position;
        Line = record.Line;
        Options = record._options;
        RawRecord = record._record.Record.SafeCopy();
        Header = record._owner.Header;

        using WritableBuffer<T> buffer = new(Options.Allocator);

        foreach (var field in record)
        {
            buffer.Push(field);
        }

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
        IMaterializer<T, TRecord> materializer = Header is not null
            ? Options.TypeBinder.GetMaterializer<TRecord>(Header.Values)
            : Options.TypeBinder.GetMaterializer<TRecord>();

        CsvPreservedRecord<T> @this = this;
        return materializer.Parse(ref @this);
    }

    /// <summary>
    /// Parses the record into an instance of <typeparamref name="TRecord"/> by using the type map.
    /// </summary>
    public TRecord ParseRecord<TRecord>(CsvTypeMap<T, TRecord> typeMap)
    {
        ArgumentNullException.ThrowIfNull(typeMap);

        IMaterializer<T, TRecord> materializer = Header is not null
            ? typeMap.GetMaterializer(Header.Values, Options)
            : typeMap.GetMaterializer(Options);

        CsvPreservedRecord<T> @this = this;
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
            if (Header is null)
            {
                Throw.NotSupported_CsvHasNoHeader();
            }

            if (!Header.TryGetValue(name, out index))
            {
                Throw.Argument_HeaderNameNotFound(name, Header.Values.AsEnumerable());
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

    ReadOnlySpan<T> ICsvRecord<T>.this[int index] => _fields[index];

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
        return converter.TryParse(GetField(id).Span, out value);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"{{ CsvRecord[{FieldCount}] \"{Options.GetAsString(RawRecord.Span)}\" }}";
    }

    int IReadOnlyCollection<KeyValuePair<CsvFieldIdentifier, ReadOnlyMemory<T>>>.Count => _fields.Length;

    IEnumerator<KeyValuePair<CsvFieldIdentifier, ReadOnlyMemory<T>>> IEnumerable<
        KeyValuePair<CsvFieldIdentifier, ReadOnlyMemory<T>>
    >.GetEnumerator()
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
            for (int i = 0; i < _fields.Length; i++)
                yield return i;
        }
    }

    IEnumerable<ReadOnlyMemory<T>> IReadOnlyDictionary<CsvFieldIdentifier, ReadOnlyMemory<T>>.Values
    {
        get
        {
            foreach (var f in _fields)
                yield return f;
        }
    }

    bool IReadOnlyDictionary<CsvFieldIdentifier, ReadOnlyMemory<T>>.ContainsKey(CsvFieldIdentifier key)
    {
        return Contains(key);
    }

    bool IReadOnlyDictionary<CsvFieldIdentifier, ReadOnlyMemory<T>>.TryGetValue(
        CsvFieldIdentifier key,
        out ReadOnlyMemory<T> value
    )
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
