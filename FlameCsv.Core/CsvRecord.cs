using System.Collections;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.HighPerformance;
using FlameCsv.Binding;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Runtime;

namespace FlameCsv;

public class CsvRecord<T> : ICsvRecord<T>, IReadOnlyList<ReadOnlyMemory<T>> where T : unmanaged, IEquatable<T>
{
    public virtual ReadOnlyMemory<T> this[int index] => GetField(index);
    public virtual ReadOnlyMemory<T> this[string name] => GetField(name);

    public virtual long Position { get; protected set; }
    public virtual int Line { get; protected set; }
    public CsvOptions<T> Options { get; }
    public virtual ReadOnlyMemory<T> RawRecord { get; }
    public bool HasHeader => _header is not null;

    private readonly ArraySegment<ReadOnlyMemory<T>> _values;
    private readonly Dictionary<string, int>? _header;
    private readonly string[]? _headerNames;

    public CsvRecord(CsvValueRecord<T> record)
    {
        Throw.IfDefaultStruct<CsvValueRecord<T>>(record._options);

        Options = record._options;
        _header = record._state.Header;
        _headerNames = record._state._headerNames;
        (RawRecord, _values) = Initialize(record);

        // we don't need to validate field count here, as a non-default CsvValueRecord
        // validates it on initialization
    }

    int IReadOnlyCollection<ReadOnlyMemory<T>>.Count => GetFieldCount();

    ReadOnlyMemory<T> IReadOnlyList<ReadOnlyMemory<T>>.this[int index] => this[index];

    [RequiresUnreferencedCode(Messages.CompiledExpressions)]
    public TRecord ParseRecord<[DynamicallyAccessedMembers(Messages.ReflectionBound)] TRecord>()
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

    public virtual ReadOnlyMemory<T> GetField(int index) => _values.AsSpan()[index];

    public virtual ReadOnlyMemory<T> GetField(string name)
    {
        if (_header is null)
            Throw.NotSupported_CsvHasNoHeader();

        return _values.AsSpan()[_header[name]];
    }

    public virtual TValue GetField<TValue>(int index)
    {
        var field = GetField(index);

        var converter = Options.GetConverter<TValue>();

        if (!converter.TryParse(field.Span, out TValue? value))
            Throw.ParseFailed(field, converter, Options, typeof(TValue));

        return value;
    }

    public virtual TValue GetField<TValue>(string name)
    {
        var field = GetField(name);

        var converter = Options.GetConverter<TValue>();

        if (!converter.TryParse(field.Span, out TValue? value))
            Throw.ParseFailed(field, converter, Options, typeof(TValue));

        return value;
    }

    public virtual int GetFieldCount() => _values.Count;

    public virtual bool TryGetValue<TValue>(int index, [MaybeNullWhen(false)] out TValue value)
    {
        if ((uint)index > _values.Count)
        {
            Throw.Argument_FieldIndex(index, _values.Count);
        }

        if (!Options.GetConverter<TValue>().TryParse(_values[index].Span, out value))
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

    private static PreservedValues Initialize(CsvValueRecord<T> record)
    {
        int count = 0;
        int totalLength = record.RawRecord.Length;

        foreach (var field in record)
        {
            count++;
            totalLength += field.Length;
        }

        // split into separate arrays if the record is really big
        if (Token<T>.LargeObjectHeapAllocates(totalLength))
        {
            var _values = new ReadOnlyMemory<T>[count];
            int _index = 0;

            foreach (var field in record)
                _values[_index++] = field.SafeCopy();

            return new PreservedValues(record.RawRecord.SafeCopy(), _values);
        }

        T[] array = GC.AllocateUninitializedArray<T>(totalLength);
        var data = new Memory<T>(array, 0, record.RawRecord.Length);
        record.RawRecord.CopyTo(data);

        int fieldIndex = 0;
        int runningIndex = data.Length;
        var values = new ReadOnlyMemory<T>[count];

        foreach (var field in record)
        {
            Memory<T> current = new(array, runningIndex, field.Length);
            field.CopyTo(current);
            runningIndex += field.Length;
            values[fieldIndex++] = current;
        }

        return new PreservedValues(data, values);
    }

    IEnumerator<ReadOnlyMemory<T>> IEnumerable<ReadOnlyMemory<T>>.GetEnumerator() => _values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<ReadOnlyMemory<T>>)this).GetEnumerator();
    private readonly record struct PreservedValues(ReadOnlyMemory<T> Record, ArraySegment<ReadOnlyMemory<T>> Fields);
}
