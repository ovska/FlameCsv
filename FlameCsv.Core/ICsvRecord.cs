using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv;

public class CsvRecord<T> : ICsvRecord<T> where T : unmanaged, IEquatable<T>
{
    public virtual ReadOnlyMemory<T> this[int index] => GetField(index);
    public virtual ReadOnlyMemory<T> this[string name] => GetField(name);

    public virtual long Position { get; protected set; }
    public virtual int Line { get; protected set; }
    public virtual CsvDialect<T> Dialect => _dialect;
    public virtual ReadOnlyMemory<T> Data => _data;

    public bool HasHeader => _header is not null;

    protected readonly CsvDialect<T> _dialect;
    protected readonly CsvReaderOptions<T> _options;
    protected readonly ReadOnlyMemory<T> _data;
    protected readonly ReadOnlyMemory<ReadOnlyMemory<T>> _values;

    private readonly Dictionary<string, int>? _header;

    public CsvRecord(CsvValueRecord<T> record)
    {
        GuardEx.EnsureNotDefaultStruct(record._options);

        _options = record._options;
        _dialect = record.Dialect;
        _header = record._state._header;
        (_data, _values) = Initialize(record);
    }

    public CsvRecord(ReadOnlyMemory<T> record, CsvReaderOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.MakeReadOnly();

        _options = options;
        _dialect = new CsvDialect<T>(options);
        (_data, _values) = InitializeFromValues(
            record,
            options,
            in _dialect,
            false);
    }

    public CsvRecord(ReadOnlySpan<T> record, CsvReaderOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.MakeReadOnly();

        _options = options;
        _dialect = new CsvDialect<T>(options);
        (_data, _values) = InitializeFromValues(Preserve(record), options, in _dialect, true);
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

        if (!_options.GetParser<TValue>().TryParse(field.Span, out TValue? value))
            throw new InvalidOperationException();

        return value;
    }

    public virtual TValue GetField<TValue>(string name)
    {
        var field = GetField(name);

        if (!_options.GetParser<TValue>().TryParse(field.Span, out TValue? value))
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
        nint totalLength = record.Data.Length;

        foreach (var field in record)
        {
            count++;
            totalLength += field.Length;
        }

        // split into separate arrays if the record is really big
        if (totalLength >= Token<T>.LOHLimit)
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
        //if (record.Length < Token<T>.LOHLimit)
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
            ArrayPool<T> arrayPool = options.ArrayPool.AllocatingIfNull();

            try
            {
                CsvEnumerationStateRef<T> state = new(options, record);

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
            finally
            {
                arrayPool.EnsureReturned(ref buffer);
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

/// <summary>
/// An instance representing a single CSV record.
/// </summary>
/// <remarks>
/// The data in the record is read lazily. Subsequent operations will use cached data if possible.<br/>
/// References to the record or its fields must not be held onto after the next record has been read.
/// Parse the data or make a copy of the data if you need to hold onto it.
/// </remarks>
/// <typeparam name="T">Token type</typeparam>
public interface ICsvRecord<T> where T : unmanaged, IEquatable<T>
{
    /// <inheritdoc cref="GetField(int)"/>
    ReadOnlyMemory<T> this[int index] { get; }

    /// <inheritdoc cref="GetField(string)"/>
    ReadOnlyMemory<T> this[string name] { get; }

    bool HasHeader { get; }

    /// <summary>
    /// 0-based token position of this record's beginning from the start of the CSV.
    /// </summary>
    /// <remarks>First record's position is always 0.</remarks>
    long Position { get; }

    /// <summary>
    /// 1-based logical line number in the CSV. The header record is counted as a line.
    /// </summary>
    /// <remarks>First record's line number is always 1.</remarks>
    int Line { get; }

    /// <summary>
    /// CSV dialect used to parse the records.
    /// </summary>
    CsvDialect<T> Dialect { get; }

    /// <summary>
    /// The complete unescaped data on the line without trailing newline tokens.
    /// </summary>
    /// <remarks>
    /// Reference to the data must not be held onto after the next record has been read.
    /// If the data is needed later, copy the data into a separate array.
    /// </remarks>
    ReadOnlyMemory<T> Data { get; }

    /// <summary>
    /// Returns the value of the field at the specified index.
    /// </summary>
    /// <remarks>
    /// Reference to the data must not be held onto after the next record has been read.
    /// If the data is needed later, copy the data into a separate array.
    /// </remarks>
    /// <param name="index">0-based column index, e.g. 0 for the first column</param>
    /// <returns>Column value, unescaped and stripped of quotes when applicable</returns>
    /// <exception cref="ArgumentOutOfRangeException"/>
    ReadOnlyMemory<T> GetField(int index);

    /// <summary>
    /// Returns the value of the field with the specified name. Requires for the CSV to have a header record.
    /// </summary>
    /// <remarks>
    /// The CSV must have a header record.<br/>
    /// Reference to the data must not be held onto after the next record has been read.
    /// If the data is needed later, copy the data into a separate array.
    /// </remarks>
    /// <param name="name">Header name to get the field for</param>
    /// <returns>Column value, unescaped and stripped of quotes when applicable</returns>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="InvalidOperationException"/>
    ReadOnlyMemory<T> GetField(string name);

    /// <summary>
    /// Returns the number of fields in the current record.
    /// </summary>
    /// <remarks>
    /// Reads the CSV record its' entirety if it already hasn't been tokenized.
    /// </remarks>
    int GetFieldCount();

    /// <inheritdoc cref="TryGetValue{TValue}(int, out TValue, out CsvGetValueReason)"/>
    bool TryGetValue<TValue>(int index, [MaybeNullWhen(false)] out TValue value);

    /// <summary>
    /// Attempts to parse a <typeparamref name="TValue"/> from field at <paramref name="index"/>.
    /// </summary>
    /// <remarks>The CSV must have a header record.</remarks>
    /// <typeparam name="TValue">Value parsed</typeparam>
    /// <param name="index">0-based field index</param>
    /// <param name="value">Parsed value, if successful</param>
    /// <param name="reason">Reason for the failure</param>
    /// <returns><see langword="true"/> if the value was successfully parsed</returns>
    bool TryGetValue<TValue>(int index, [MaybeNullWhen(false)] out TValue value, out CsvGetValueReason reason);

    /// <inheritdoc cref="TryGetValue{TValue}(string, out TValue, out CsvGetValueReason)"/>
    bool TryGetValue<TValue>(string name, [MaybeNullWhen(false)] out TValue value);

    /// <summary>
    /// Attempts to parse a <typeparamref name="TValue"/> from field at the specified column.
    /// </summary>
    /// <remarks>The CSV must have a header record.</remarks>
    /// <typeparam name="TValue">Value parsed</typeparam>
    /// <param name="name">Header name to get the field for</param>
    /// <param name="value">Parsed value, if successful</param>
    /// <param name="reason">Reason for the failure</param>
    /// <returns><see langword="true"/> if the value was successfully parsed</returns>
    bool TryGetValue<TValue>(string name, [MaybeNullWhen(false)] out TValue value, out CsvGetValueReason reason);

    /// <summary>
    /// Parses a value of type <typeparamref name="TValue"/> from field at <paramref name="index"/>.
    /// </summary>
    /// <typeparam name="TValue">Value parsed</typeparam>
    /// <param name="index">0-based field index</param>
    /// <returns>Parsed value</returns>
    /// <exception cref="ArgumentOutOfRangeException"/>
    /// <exception cref="CsvParserMissingException"/>
    /// <exception cref="CsvParseException"/>
    TValue GetField<TValue>(int index);

    /// <summary>
    /// Parses a value of type <typeparamref name="TValue"/> from field at the specified column.
    /// </summary>
    /// <remarks>The CSV must have a header record.</remarks>
    /// <typeparam name="TValue">Value parsed</typeparam>
    /// <param name="name">Header name to get the field for</param>
    /// <returns>Parsed value</returns>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="InvalidOperationException"/>
    /// <exception cref="CsvParserMissingException"/>
    /// <exception cref="CsvParseException"/>
    TValue GetField<TValue>(string name);
}

