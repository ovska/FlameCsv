using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text;
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
    public virtual CsvDialect<T> Dialect => _context.Dialect;
    public CsvReaderOptions<T> Options => _context.Options;
    public bool HasHeader => _context.HasHeader;
    public virtual ReadOnlyMemory<T> RawRecord { get; }

    private readonly CsvReadingContext<T> _context;
    private readonly ArraySegment<ReadOnlyMemory<T>> _values;
    private readonly Dictionary<string, int>? _header;

    public CsvRecord(CsvValueRecord<T> record)
    {
        Throw.IfDefaultStruct<CsvValueRecord<T>>(record._options);

        _context = record._state._context;
        _header = record._state.Header;
        (RawRecord, _values) = Initialize(record);

        if (Options.ValidateFieldCount && (_values.Count != record._state._expectedFieldCount))
        {
            if (!record._state._expectedFieldCount.HasValue)
                Throw.InvalidOp_DefaultStruct(record.GetType());

            Throw.InvalidData_FieldCount(_values.Count, record._state._expectedFieldCount ?? -1);
        }
    }

    public CsvRecord(
        string record,
        CsvReaderOptions<T> options,
        CsvContextOverride<T> context = default)
        : this(
              typeof(T) == typeof(char) ? (ReadOnlyMemory<T>)(object)record.AsMemory() :
              typeof(T) == typeof(byte) ? (ReadOnlyMemory<T>)(object)Encoding.UTF8.GetBytes(record) :
              throw new NotSupportedException(),
              options,
              context)
    {
    }

    public IEnumerable<string> GetHeaderRecord()
    {
        if (!_context.HasHeader)
            Throw.NotSupported_CsvHasNoHeader();

        if (_header is null)
            Throw.InvalidOperation_HeaderNotRead();

        return _header.Keys;
    }

    public CsvRecord(ReadOnlyMemory<T> record, CsvReaderOptions<T> options, CsvContextOverride<T> context = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.MakeReadOnly();

        _context = new CsvReadingContext<T>(options, context);
        (RawRecord, _values) = InitializeFromValues(record, in _context);
    }

    int IReadOnlyCollection<ReadOnlyMemory<T>>.Count => GetFieldCount();
    ReadOnlyMemory<T> IReadOnlyList<ReadOnlyMemory<T>>.this[int index] => this[index];

    public TRecord ParseRecord<TRecord>()
    {
        IMaterializer<T, TRecord> materializer;

        if (_header is not null)
        {
            var bindings = _context.Options.GetHeaderBinder().Bind<TRecord>(_header.Keys);
            materializer = _context.Options.CreateMaterializerFrom(bindings);
        }
        else
        {
            materializer = _context.Options.GetMaterializer<T, TRecord>();
        }

        return materializer.Parse(_values);
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

        var parser = Options.GetParser<TValue>();

        if (!parser.TryParse(field.Span, out TValue? value))
            Throw.ParseFailed<T,TValue>(field, parser, in _context);
        
        return value;
    }

    public virtual TValue GetField<TValue>(string name)
    {
        var field = GetField(name);

        var parser = Options.GetParser<TValue>();

        if (!parser.TryParse(field.Span, out TValue? value))
            Throw.ParseFailed<T, TValue>(field, parser, in _context);

        return value;
    }

    public virtual int GetFieldCount() => _values.Count;

    public virtual bool TryGetValue<TValue>(int index, [MaybeNullWhen(false)] out TValue value)
    {
        if ((uint)index > _values.Count)
        {
            Throw.Argument_FieldIndex(index, _values.Count);
        }

        if (!Options.GetParser<TValue>().TryParse(_values[index].Span, out value))
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
            Throw.Argument_HeaderNameNotFound(name, _context.ExposeContent, _header.Keys);
        }

        return TryGetValue<TValue>(index, out value);
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

        var array = new T[totalLength];
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

    private static PreservedValues InitializeFromValues(
        ReadOnlyMemory<T> record,
        in CsvReadingContext<T> context)
    {
        //if (!Token<T>.LargeObjectHeapAllocates(record.Length * 2))
        //{
        //    T[]? buffer = null;
        //    ArrayPool<T> arrayPool = context.ArrayPool;

        //    // use a single buffer for everything
        //    Memory<T> remaining = new T[record.Length * 2];
        //    record.CopyTo(remaining);
        //    remaining = remaining.Slice(record.Length);

        //    try
        //    {
        //        CsvEnumerationStateRef<T> state = new(in context, record, ref buffer);

        //        while (!state.remaining.IsEmpty)
        //        {
        //            var field = context.ReadNextField(ref state);
        //            field.CopyTo(remaining);
        //            remaining = remaining.Slice(field.Length);
        //        }

        //        return new PreservedValues(record.SafeCopy(),
        //            values.AsMemory(0, index));
        //    }
        //    finally
        //    {
        //        arrayPool.EnsureReturned(ref buffer);
        //    }
        //}
        //else
        {
            ReadOnlyMemory<T>[] values = new ReadOnlyMemory<T>[16];
            int index = 0;
            T[]? buffer = null;

            // TODO!!!
            using (CsvEnumerationStateRef<T>.CreateTemporary(in context, record, ref buffer, out var state))
            {
                while (!state.remaining.IsEmpty)
                {
                    if (index >= values.Length)
                        Array.Resize(ref values, values.Length * 2);

                    values[index++] = context.ReadNextField(ref state).SafeCopy();
                }

                return new PreservedValues(
                    record.SafeCopy(),
                    new ArraySegment<ReadOnlyMemory<T>>(values, 0, index));
            }
        }
    }

    IEnumerator<ReadOnlyMemory<T>> IEnumerable<ReadOnlyMemory<T>>.GetEnumerator()
    {
        foreach (var field in _values)
        {
            yield return field;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<ReadOnlyMemory<T>>)this).GetEnumerator();

    private readonly struct PreservedValues
    {
        public PreservedValues(ReadOnlyMemory<T> record, ArraySegment<ReadOnlyMemory<T>> fields)
        {
            Record = record;
            Fields = fields;
        }

        public ReadOnlyMemory<T> Record { get; }
        public ArraySegment<ReadOnlyMemory<T>> Fields { get; }

        public void Deconstruct(out ReadOnlyMemory<T> record, out ArraySegment<ReadOnlyMemory<T>> fields)
        {
            record = Record;
            fields = Fields;
        }
    }
}

