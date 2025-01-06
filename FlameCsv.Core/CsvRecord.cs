using System.Collections;
using System.Diagnostics.CodeAnalysis;
using FlameCsv.Binding;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Runtime;

namespace FlameCsv;

public class CsvRecord<T> : ICsvRecord<T>, IReadOnlyList<ReadOnlyMemory<T>> where T : unmanaged, IEquatable<T>
{
    public virtual ReadOnlyMemory<T> this[int index] => GetField(index);
    public virtual ReadOnlyMemory<T> this[string name] => GetField(name);

    public virtual long Position { get; }
    public virtual int Line { get; }
    public CsvOptions<T> Options { get; }
    public virtual ReadOnlyMemory<T> RawRecord { get; }

    public bool HasHeader => _header is not null;

    private readonly ReadOnlyMemory<T>[] _fields;
    private readonly Dictionary<string, int>? _header;
    private readonly string[]? _headerNames;

    public CsvRecord(in CsvValueRecord<T> record)
    {
        Throw.IfDefaultStruct(record._options is null, typeof(CsvValueRecord<T>));

        // we don't need to validate field count here, as a non-default CsvValueRecord validates it on init
        Position = record.Position;
        Line = record.Line;
        Options = record._options;
        _header = record._state.Header;
        _headerNames = record._state._headerNames;

        RawRecord = record.RawRecord.SafeCopy();
        _fields = record._state.PreserveFields();
    }

    int IReadOnlyCollection<ReadOnlyMemory<T>>.Count => GetFieldCount();

    ReadOnlyMemory<T> IReadOnlyList<ReadOnlyMemory<T>>.this[int index] => this[index];

    [RUF(Messages.CompiledExpressions), RDC(Messages.CompiledExpressions)]
    public TRecord ParseRecord<[DAM(Messages.ReflectionBound)] TRecord>()
    {
        IMaterializer<T, TRecord> materializer;

        if (_header is not null)
        {
            var bindings = Options.GetHeaderBinder().Bind<TRecord>(_headerNames);
            materializer = Options.CreateMaterializerFrom(bindings);
        }
        else
        {
            materializer = Options.GetMaterializer<T, TRecord>();
        }

        return Options.Materialize(RawRecord, materializer);
    }

    public TRecord ParseRecord<TRecord>(CsvTypeMap<T, TRecord> typeMap)
    {
        ArgumentNullException.ThrowIfNull(typeMap);

        IMaterializer<T, TRecord> materializer = _header is not null
            ? typeMap.BindMembers(_headerNames, Options)
            : typeMap.BindMembers(Options);

        return Options.Materialize(RawRecord, materializer);
    }

    public virtual ReadOnlyMemory<T> GetField(int index) => _fields[index]; // TODO: slice directly from array

    public virtual ReadOnlyMemory<T> GetField(string name)
    {
        if (_header is null)
            Throw.NotSupported_CsvHasNoHeader();

        return _fields[_header[name]];
    }

    public virtual TValue GetField<TValue>(int index)
    {
        var field = GetField(index).Span;

        var converter = Options.GetConverter<TValue>();

        if (!converter.TryParse(field, out TValue? value))
            Throw.ParseFailed(field, converter, Options, typeof(TValue));

        return value;
    }

    public virtual TValue GetField<TValue>(string name)
    {
        var field = GetField(name).Span;

        var converter = Options.GetConverter<TValue>();

        if (!converter.TryParse(field, out TValue? value))
            Throw.ParseFailed(field, converter, Options, typeof(TValue));

        return value;
    }

    public virtual int GetFieldCount() => _fields.Length;

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
            Throw.Argument_HeaderNameNotFound(name, Options.AllowContentInExceptions, _header.Keys);
        }

        return TryGetValue(index, out value);
    }

    public IEnumerable<string> GetHeaderRecord()
    {
        if (!Options.HasHeader)
            Throw.NotSupported_CsvHasNoHeader();

        if (_header is null)
            Throw.InvalidOperation_HeaderNotRead();

        return _header.Keys;
    }

    IEnumerator<ReadOnlyMemory<T>> IEnumerable<ReadOnlyMemory<T>>.GetEnumerator()
        => _fields.AsEnumerable().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<ReadOnlyMemory<T>>)this).GetEnumerator();
}
