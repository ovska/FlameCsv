using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
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
    public virtual ReadOnlyMemory<T> Data { get; }
    public CsvReaderOptions<T> Options { get; }
    public bool HasHeader => _header is not null;

    int IReadOnlyCollection<ReadOnlyMemory<T>>.Count => GetFieldCount();
    ReadOnlyMemory<T> IReadOnlyList<ReadOnlyMemory<T>>.this[int index] => this[index];

    private readonly ReadOnlyMemory<ReadOnlyMemory<T>> _values;
    private readonly Dictionary<string, int>? _header;

    public CsvRecord(CsvValueRecord<T> record)
    {
        GuardEx.EnsureNotDefaultStruct(record._options);

        Options = record._options;
        Dialect = record.Dialect;
        _header = record._state._header;
        (Data, _values) = Initialize(record);
    }

    public CsvRecord(ReadOnlyMemory<T> record, CsvReaderOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.MakeReadOnly();

        CsvDialect<T> dialect = new(options);

        Options = options;
        (Data, _values) = InitializeFromValues(
            record,
            options,
            in dialect,
            false);
    }

    public CsvRecord(ReadOnlySpan<T> record, CsvReaderOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.MakeReadOnly();

        CsvDialect<T> dialect = new(options);

        Options = options;
        Dialect = dialect;
        (Data, _values) = InitializeFromValues(Preserve(record), options, in dialect, true);
    }

    public TRecord ParseRecord<TRecord>()
    {
        throw new NotImplementedException();
    }

    public virtual ReadOnlyMemory<T> GetField(int index) => _values.Span[index];

    public virtual ReadOnlyMemory<T> GetField(string name)
    {
        EnsureHeader();
        return _values.Span[_header[name]];
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

    public virtual int GetFieldCount() => _values.Length;

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

    /// <summary>
    /// Throws an <see cref="NotSupportedException"/> if the record does not have a header.
    /// </summary>
    [MemberNotNull(nameof(_header))]
    protected void EnsureHeader()
    {
        if (_header is null)
        {
            ThrowHelper.ThrowNotSupportedException("The current CSV does not have a header record.");
        }
    }

    private static PreservedValues Initialize(CsvValueRecord<T> record)
    {
        int count = 0;
        int totalLength = record.Data.Length;

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
                _values[_index++] = Preserve(field.Span);

            return new PreservedValues(Preserve(record.Data.Span), _values);
        }

        var array = new T[totalLength];
        var data = new Memory<T>(array, 0, record.Data.Length);
        record.Data.CopyTo(data);

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
        in CsvDialect<T> dialect,
        bool recordPreserved)
    {
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
        //        CsvEnumerationStateRef<T> state = new(options, record);

        //        while (!state.remaining.IsEmpty)
        //        {
        //            var field = state.ReadNextField();
        //            field.CopyTo(remaining);
        //            remaining = remaining.Slice(field.Length);
        //        }

        //        return new PreservedValues(
        //            recordPreserved ? record : Preserve(record.Span),
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

            using (CsvEnumerationStateRefLifetime<T>.Create(options, record, ref buffer, out var state))
            {
                while (!state.remaining.IsEmpty)
                {
                    if (index >= values.Length)
                        Array.Resize(ref values, values.Length * 2);

                    values[index++] = Preserve(state.ReadNextField().Span);
                }

                return new PreservedValues(
                    recordPreserved ? record : Preserve(record.Span),
                    values.AsMemory(0, index));
            }
        }
    }

    private static ReadOnlyMemory<T> Preserve(ReadOnlySpan<T> other)
    {
        if (typeof(T) == typeof(char))
        {
            var str = other.ToString().AsMemory();
            return Unsafe.As<ReadOnlyMemory<char>, ReadOnlyMemory<T>>(ref str);
        }

        return other.ToArray();
    }

    IEnumerator<ReadOnlyMemory<T>> IEnumerable<ReadOnlyMemory<T>>.GetEnumerator()
    {
        throw new NotImplementedException();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        throw new NotImplementedException();
    }

    private readonly struct PreservedValues
    {
        public PreservedValues(ReadOnlyMemory<T> record, ReadOnlyMemory<ReadOnlyMemory<T>> fields)
        {
            Record = record;
            Fields = fields;
        }

        public ReadOnlyMemory<T> Record { get; }
        public ReadOnlyMemory<ReadOnlyMemory<T>> Fields { get; }

        public void Deconstruct(out ReadOnlyMemory<T> record, out ReadOnlyMemory<ReadOnlyMemory<T>> fields)
        {
            record = Record;
            fields = Fields;
        }
    }
}

