using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv;

public class CsvRecord<T> : ICsvRecord<T>, IReadOnlyList<ReadOnlyMemory<T>> where T : unmanaged, IEquatable<T>
{
    public virtual ReadOnlyMemory<T> this[int index] => GetField(index);
    public virtual ReadOnlyMemory<T> this[string name] => GetField(name);

    public virtual long Position { get; protected set; }
    public virtual int Line { get; protected set; }
    public virtual CsvDialect<T> Dialect { get; }
    public virtual ReadOnlyMemory<T> RawRecord { get; }
    public CsvReaderOptions<T> Options { get; }
    public bool HasHeader => _header is not null;

    int IReadOnlyCollection<ReadOnlyMemory<T>>.Count => GetFieldCount();
    ReadOnlyMemory<T> IReadOnlyList<ReadOnlyMemory<T>>.this[int index] => this[index];

    private readonly ArraySegment<ReadOnlyMemory<T>> _values;
    private readonly Dictionary<string, int>? _header;

    public CsvRecord(CsvValueRecord<T> record)
    {
        Throw.IfDefaultStruct<CsvValueRecord<T>>(record._options);

        Options = record._options;
        Dialect = record._state.Dialect;
        _header = record._state.Header;
        (RawRecord, _values) = Initialize(record);

        if (Options.ValidateFieldCount && (_values.Count != record._state._expectedFieldCount))
        {
            if (!record._state._expectedFieldCount.HasValue)
                Throw.InvalidOp_DefaultStruct(record.GetType());

            Throw.InvalidData_FieldCount(_values.Count, record._state._expectedFieldCount ?? -1);
        }
    }

    public CsvRecord(ReadOnlyMemory<T> record, CsvReaderOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.MakeReadOnly();

        Options = options;
        (RawRecord, _values) = InitializeFromValues(record, options, false);
    }

    public TRecord ParseRecord<TRecord>()
    {
        throw new NotImplementedException();
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

        if (!Options.GetParser<TValue>().TryParse(field.Span, out TValue? value))
            throw new InvalidOperationException();

        return value;
    }

    public virtual TValue GetField<TValue>(string name)
    {
        var field = GetField(name);

        if (!Options.GetParser<TValue>().TryParse(field.Span, out TValue? value))
            throw new InvalidOperationException();

        return value;
    }

    public virtual int GetFieldCount() => _values.Count;

    public virtual bool TryGetValue<TValue>(int index, [MaybeNullWhen(false)] out TValue value)
    {
        throw new NotImplementedException();
    }

    public virtual bool TryGetValue<TValue>(int index, [MaybeNullWhen(false)] out TValue value, out CsvGetValueReason reason)
    {
        throw new NotImplementedException();
    }

    public virtual bool TryGetValue<TValue>(string name, [MaybeNullWhen(false)] out TValue value)
    {
        throw new NotImplementedException();
    }

    public virtual bool TryGetValue<TValue>(string name, [MaybeNullWhen(false)] out TValue value, out CsvGetValueReason reason)
    {
        throw new NotImplementedException();
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
        CsvReaderOptions<T> options,
        bool recordPreserved)
    {
        CsvReadingContext<T> context = new(options);

        //if (!Token<T>.LargeObjectHeapAllocates(record.Length * 2))
        //{
        //    T[]? buffer = null;
        //    ArrayPool<T> arrayPool = options.ArrayPool.AllocatingIfNull();

        //    // use a single buffer for everything
        //    Memory<T> remaining;

        //    if (recordPreserved)
        //    {
        //        remaining = new T[record.Length];
        //    }
        //    else
        //    {
        //        remaining = new T[record.Length * 2];
        //        record.CopyTo(remaining);
        //        remaining = remaining.Slice(record.Length);
        //    }

        //    try
        //    {
        //        CsvEnumerationStateRef<T> state = new(in context, record, ref buffer);

        //        while (!state.remaining.IsEmpty)
        //        {
        //            var field = context.ReadNextField(ref state);
        //            field.CopyTo(remaining);
        //            remaining = remaining.Slice(field.Length);
        //        }

        //        return new PreservedValues(
        //            recordPreserved ? record : record.SafeCopy(),
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
            using (CsvEnumerationStateRefLifetime<T>.Create(in context, record, ref buffer, out var state))
            {
                while (!state.remaining.IsEmpty)
                {
                    if (index >= values.Length)
                        Array.Resize(ref values, values.Length * 2);

                    values[index++] = context.ReadNextField(ref state).SafeCopy();
                }

                return new PreservedValues(
                    recordPreserved ? record : record.SafeCopy(),
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

