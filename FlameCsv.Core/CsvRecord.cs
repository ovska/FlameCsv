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
    public virtual ReadOnlyMemory<T> this[int index] => GetField(index);

    /// <inheritdoc/>
    public virtual ReadOnlyMemory<T> this[string name] => GetField(name);

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
    [RUF(Messages.CompiledExpressions), RDC(Messages.CompiledExpressions)]
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
    public virtual ReadOnlyMemory<T> GetField(int index) => _fields[index];

    /// <inheritdoc/>
    public virtual ReadOnlyMemory<T> GetField(string name)
    {
        if (_header is null)
            Throw.NotSupported_CsvHasNoHeader();

        return _fields[_header[name]];
    }

    /// <inheritdoc/>
    public virtual TValue GetField<TValue>(int index)
    {
        var field = GetField(index).Span;

        var converter = Options.GetConverter<TValue>();

        if (!converter.TryParse(field, out TValue? value))
            Throw.ParseFailed(field, converter, typeof(TValue));

        return value;
    }

    /// <inheritdoc/>
    public virtual TValue GetField<TValue>(string name)
    {
        var field = GetField(name).Span;

        var converter = Options.GetConverter<TValue>();

        if (!converter.TryParse(field, out TValue? value))
            Throw.ParseFailed(field, converter, typeof(TValue));

        return value;
    }

    /// <inheritdoc/>
    public virtual int GetFieldCount() => _fields.Length;

    /// <inheritdoc/>
    public virtual bool TryGetValue<TValue>(int index, [MaybeNullWhen(false)] out TValue value)
    {
        if ((uint)index > _fields.Length)
        {
            Throw.Argument_FieldIndex(index, _fields.Length);
        }

        if (!Options.GetConverter<TValue>().TryParse(_fields[index].Span, out value))
        {
            value = default;
            return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public virtual bool TryGetValue<TValue>(string name, [MaybeNullWhen(false)] out TValue value)
    {
        if (!HasHeader)
        {
            Throw.NotSupported_CsvHasNoHeader();
        }

        if (_header is null)
        {
            Throw.InvalidOperation_HeaderNotRead();
        }

        if (!_header.TryGetValue(name, out int index))
        {
            Throw.Argument_HeaderNameNotFound(name, _header.HeaderNames);
        }

        return TryGetValue(index, out value);
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
        public int FieldCount => record.GetFieldCount();
        public ReadOnlySpan<T> this[int index] => record._fields[index].Span;
    }
}
