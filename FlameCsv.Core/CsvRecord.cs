using System.Collections;
using System.Diagnostics.CodeAnalysis;
using FlameCsv.Binding;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv;

/// <summary>
/// Represents a single CSV record.
/// </summary>
/// <remarks>
/// This class is a self-contained copy of a CSV record.
/// </remarks>
public class CsvRecord<T> : ICsvRecord<T>, IReadOnlyList<ReadOnlyMemory<T>> where T : unmanaged, IBinaryInteger<T>
{
    /// <inheritdoc/>
    public virtual ReadOnlyMemory<T> this[CsvFieldIdentifier id] => GetField(id);

    /// <inheritdoc/>
    public virtual long Position { get; }

    /// <inheritdoc/>
    public virtual int Line { get; }

    /// <summary>
    /// Current options instance.
    /// </summary>
    public CsvOptions<T> Options { get; }

    /// <inheritdoc/>
    public virtual ReadOnlyMemory<T> RawRecord { get; }

    /// <inheritdoc/>
    [MemberNotNullWhen(true, nameof(_header))]
    public bool HasHeader => _header is not null;

    /// <inheritdoc/>
    public bool Contains(CsvFieldIdentifier id)
    {
        return id.TryGetIndex(out int index, out string? name)
            ? (uint)index < (uint)_fields.Length
            : HasHeader && _header.ContainsKey(name);
    }

    /// <summary>
    /// Returns the headers in the current CSV.
    /// </summary>
    /// <seealso cref="HasHeader"/>
    /// <exception cref="NotSupportedException">Options has been configured to read headered CSV</exception>
    public ReadOnlySpan<string> Header
    {
        get
        {
            if (!Options._hasHeader)
                Throw.NotSupported_CsvHasNoHeader();

            if (!HasHeader)
                Throw.InvalidOperation_HeaderNotRead();

            return _header.Values;
        }
    }

    private readonly ReadOnlyMemory<T>[] _fields;
    private readonly CsvHeader? _header;

    /// <summary>
    /// Initializes a new instance, copying data from the record.
    /// </summary>
    public CsvRecord(in CsvValueRecord<T> record)
    {
        Throw.IfDefaultStruct(record._options is null, typeof(CsvValueRecord<T>));

        // we don't need to validate field count here, as a non-default CsvValueRecord validates it on init
        Position = record.Position;
        Line = record.Line;
        Options = record._options;
        RawRecord = record.RawRecord.SafeCopy();
        _header = record._state.Header;
        _fields = record._state.PreserveFields();
    }

    int IReadOnlyCollection<ReadOnlyMemory<T>>.Count => GetFieldCount();

    ReadOnlyMemory<T> IReadOnlyList<ReadOnlyMemory<T>>.this[int index] => this[index];

    /// <inheritdoc/>
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    public TRecord ParseRecord<[DAM(Messages.ReflectionBound)] TRecord>()
    {
        IMaterializer<T, TRecord> materializer = _header is not null
            ? Options.TypeBinder.GetMaterializer<TRecord>(_header.Values)
            : Options.TypeBinder.GetMaterializer<TRecord>();

        FieldEnumerator enumerator = new(this);
        return materializer.Parse(ref enumerator);
    }

    /// <inheritdoc/>
    public TRecord ParseRecord<TRecord>(CsvTypeMap<T, TRecord> typeMap)
    {
        ArgumentNullException.ThrowIfNull(typeMap);

        IMaterializer<T, TRecord> materializer = _header is not null
            ? typeMap.GetMaterializer(_header.Values, Options)
            : typeMap.GetMaterializer(Options);

        FieldEnumerator enumerator = new(this);
        return materializer.Parse(ref enumerator);
    }

    /// <inheritdoc/>
    public virtual ReadOnlyMemory<T> GetField(CsvFieldIdentifier id)
    {
        if (!id.TryGetIndex(out int index, out string? name))
        {
            if (!HasHeader)
            {
                Throw.NotSupported_CsvHasNoHeader();
            }

            if (_header is null)
            {
                Throw.InvalidOperation_HeaderNotRead();
            }

            if (!_header.TryGetValue(name, out index))
            {
                Throw.Argument_HeaderNameNotFound(name, _header.HeaderNames);
            }
        }

        if ((uint)index >= _fields.Length)
        {
            Throw.Argument_FieldIndex(index, _fields.Length);
        }

        return _fields[index];
    }

    /// <inheritdoc/>
    public TValue ParseField<TValue>(CsvConverter<T, TValue> converter, CsvFieldIdentifier id)
    {
        ArgumentNullException.ThrowIfNull(converter);

        var field = GetField(id).Span;

        if (!converter.TryParse(field, out TValue? value))
        {
            Throw.ParseFailed(field, converter, typeof(TValue));
        }

        return value;
    }

    /// <inheritdoc/>
    [RUF(Messages.ConverterOverload), RDC(Messages.ConverterOverload)]
    public virtual TValue ParseField<TValue>(CsvFieldIdentifier id)
    {
        var field = GetField(id).Span;

        var converter = Options.GetConverter<TValue>();

        if (!converter.TryParse(field, out TValue? value))
        {
            Throw.ParseFailed(field, converter, typeof(TValue));
        }

        return value;
    }

    /// <inheritdoc/>
    public virtual int GetFieldCount() => _fields.Length;

    /// <inheritdoc/>
    [RUF(Messages.ConverterOverload), RDC(Messages.ConverterOverload)]
    public virtual bool TryParseField<TValue>(CsvFieldIdentifier id, [MaybeNullWhen(false)] out TValue value)
    {
        var converter = Options.GetConverter<TValue>();
        return converter.TryParse(GetField(id).Span, out value);
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

    IEnumerator<ReadOnlyMemory<T>> IEnumerable<ReadOnlyMemory<T>>.GetEnumerator()
        => _fields.AsEnumerable().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<ReadOnlyMemory<T>>)this).GetEnumerator();

    /// <summary>
    /// Internal implementation detail.
    /// </summary>
    /// <param name="record"></param>
    protected readonly struct FieldEnumerator(CsvRecord<T> record) : ICsvRecordFields<T>
    {
        /// <inheritdoc cref="ICsvRecordFields{T}.FieldCount"/>
        public int FieldCount => record.GetFieldCount();

        /// <inheritdoc cref="ICsvRecordFields{T}.this"/>
        public ReadOnlySpan<T> this[int index] => record._fields[index].Span;
    }
}
