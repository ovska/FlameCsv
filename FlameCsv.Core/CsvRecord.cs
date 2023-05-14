using System.Collections;
using System.Diagnostics.CodeAnalysis;
using FlameCsv.Binding;
using FlameCsv.Exceptions;
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
    public CsvOptions<T> Options => _context.Options;
    public bool HasHeader => _context.HasHeader;
    public virtual ReadOnlyMemory<T> RawRecord { get; }

    private readonly CsvReadingContext<T> _context;
    private readonly ArraySegment<ReadOnlyMemory<T>> _values;
    private readonly Dictionary<string, int>? _header;
    private readonly string[]? _headerNames;

    public CsvRecord(CsvValueRecord<T> record)
    {
        Throw.IfDefaultStruct<CsvValueRecord<T>>(record._options);

        _context = record._state._context;
        _header = record._state.Header;
        _headerNames = record._state._headerNames;
        (RawRecord, _values) = Initialize(record);

        // we don't need to validate field count here, as a non-default CsvValueRecord
        // validates it on initialization
    }

    public CsvRecord(ReadOnlyMemory<T> record, CsvOptions<T> options, CsvContextOverride<T> context = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.MakeReadOnly();

        _context = new CsvReadingContext<T>(options, in context);
        (RawRecord, _values) = InitializeFromValues(record, in _context);
    }

    int IReadOnlyCollection<ReadOnlyMemory<T>>.Count => GetFieldCount();
    ReadOnlyMemory<T> IReadOnlyList<ReadOnlyMemory<T>>.this[int index] => this[index];

    [RequiresUnreferencedCode(Messages.CompiledExpressions)]
    public TRecord ParseRecord<[DynamicallyAccessedMembers(Messages.ReflectionBound)] TRecord>()
    {
        IMaterializer<T, TRecord> materializer;

        if (_header is not null)
        {
            var bindings = _context.Options.GetHeaderBinder().Bind<TRecord>(_headerNames);
            materializer = _context.Options.CreateMaterializerFrom(bindings);
        }
        else
        {
            materializer = _context.Options.GetMaterializer<T, TRecord>();
        }

        CsvRecordFieldReader<T> reader = new(_values, in _context);
        return materializer.Parse(ref reader);
    }

    public TRecord ParseRecord<TRecord>(CsvTypeMap<T, TRecord> typeMap)
    {
        ArgumentNullException.ThrowIfNull(typeMap);

        IMaterializer<T, TRecord> materializer = _header is not null
            ? typeMap.GetMaterializer(_headerNames, in _context)
            : typeMap.GetMaterializer(in _context);

        CsvRecordFieldReader<T> reader = new(_values, in _context);
        return materializer.Parse(ref reader);
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

        var parser = Options.GetConverter<TValue>();

        if (!parser.TryParse(field.Span, out TValue? value))
            Throw.ParseFailed<T, TValue>(field, parser, in _context);

        return value;
    }

    public virtual TValue GetField<TValue>(string name)
    {
        var field = GetField(name);

        var parser = Options.GetConverter<TValue>();

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
            Throw.Argument_HeaderNameNotFound(name, _context.ExposeContent, _header.Keys);
        }

        return TryGetValue(index, out value);
    }

    public IEnumerable<string> GetHeaderRecord()
    {
        if (!_context.HasHeader)
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
        if (!Token<T>.LargeObjectHeapAllocates(record.Length * 2))
        {
            // use a single buffer for everything
            Memory<T> remaining = new T[record.Length * 2];
            record.CopyTo(remaining);
            ReadOnlyMemory<T> copiedRecord = remaining.Slice(0, record.Length);
            remaining = remaining.Slice(record.Length);

            ReadOnlyMemory<T>[] values = new ReadOnlyMemory<T>[16];
            int index = 0;
            T[]? buffer = null;

            using (CsvEnumerationStateRef<T>.CreateTemporary(in context, record, ref buffer, out var state))
            {
                while (state.TryReadNext(out ReadOnlyMemory<T> field))
                {
                    field.CopyTo(remaining);
                    values[index++] = remaining.Slice(0, field.Length);
                    remaining = remaining.Slice(field.Length);
                }

                return new PreservedValues(copiedRecord, new ArraySegment<ReadOnlyMemory<T>>(values, 0, index));
            }
        }
        else
        {
            ReadOnlyMemory<T>[] values = new ReadOnlyMemory<T>[16];
            int index = 0;
            T[]? buffer = null;

            using (CsvEnumerationStateRef<T>.CreateTemporary(in context, record, ref buffer, out var state))
            {
                while (state.TryReadNext(out ReadOnlyMemory<T> field))
                {
                    if (index >= values.Length)
                        Array.Resize(ref values, values.Length * 2);

                    values[index++] = field.SafeCopy();
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

internal struct CsvRecordFieldReader<T> : ICsvFieldReader<T> where T : unmanaged, IEquatable<T>
{
    private readonly ArraySegment<ReadOnlyMemory<T>> _values;
    private readonly CsvReadingContext<T> _context;
    private int _index;

    public CsvRecordFieldReader(ArraySegment<ReadOnlyMemory<T>> values, in CsvReadingContext<T> context)
    {
        _values = values;
        _context = context;
    }

    public readonly void EnsureFullyConsumed(int fieldCount)
    {
        if (_index != _values.Count)
            Throw.InvalidData_FieldCount(fieldCount, _values.Count);
    }

    [DoesNotReturn]
    public readonly void ThrowForInvalidEOF()
    {
        Throw.InvalidData_FieldCount();
    }

    [DoesNotReturn]
    public void ThrowParseFailed(ReadOnlyMemory<T> field, CsvConverter<T>? parser)
    {
        string withStr = parser is null ? "" : $" with {parser.GetType()}";

        throw new CsvParseException(
            $"Failed to parse{withStr} from {_context.AsPrintableString(field.Span)}.")
        { Parser = parser };
    }

    public readonly void TryEnsureFieldCount(int fieldCount)
    {
        if (_values.Count != fieldCount)
            Throw.InvalidData_FieldCount(fieldCount, _values.Count);
    }

    public bool TryReadNext(out ReadOnlyMemory<T> field)
    {
        if ((uint)_index < (uint)_values.Count)
        {
            field = _values[_index++];
            return true;
        }

        _index = -1;
        field = default;
        return false;
    }
}

