using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Configuration;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;

namespace FlameCsv;

/// <inheritdoc cref="ICsvRecord{T}"/>
[DebuggerTypeProxy(typeof(CsvRecord<>.CsvRecordDebugView))]
public readonly partial struct CsvRecord<T> : ICsvRecord<T> where T : unmanaged, IEquatable<T>
{
    public long Position { get; }
    public int Line { get; }
    public ReadOnlyMemory<T> Data { get; }
    public CsvDialect<T> Dialect => _state.Dialect;

    public ReadOnlyMemory<T> this[int index] => GetField(index);

    private readonly CsvEnumerationState<T> _state;
    private readonly CsvReaderOptions<T> _options;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvRecord(
        long position,
        int line,
        ReadOnlyMemory<T> data,
        CsvReaderOptions<T> options,
        int quoteCount,
        CsvEnumerationState<T> state)
    {
        Position = position;
        Line = line;
        Data = data;
        _options = options;
        _state = state;
        _state.Initialize(data, quoteCount);
    }

    /// <summary>
    /// Initializes a new CSV record using the specified data.
    /// </summary>
    /// <remarks>
    /// Creating a record this way always causes an allocation for the data if it contains quotes.
    /// To read multiple CSV records with efficient memory usage, use the GetEnumerable-methods on <see cref="CsvReader"/>.
    /// </remarks>
    public CsvRecord(
        ReadOnlyMemory<T> data,
        CsvReaderOptions<T> options,
        int line = 1,
        long position = 0) :
            this(
                position,
                line,
                data,
                options ?? throw new ArgumentNullException(nameof(options)),
                data.Span.Count(options.Delimiter),
                new CsvEnumerationState<T>(new CsvDialect<T>(options), arrayPool: null))
    {
        // ^ obs: this ctor always foregoes array pooling as the record itself isn't disposable
        Guard.IsGreaterThanOrEqualTo(line, 1);
        Guard.IsGreaterThanOrEqualTo(position, 0L);
    }

    /// <inheritdoc cref="ICsvRecord{T}.GetField(string)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyMemory<T> GetField(string name)
    {
        if (!_state.TryGetHeaderIndex(name, out int index))
        {
            ThrowHeaderException(name);
        }

        return GetField(index);
    }

    /// <inheritdoc cref="ICsvRecord{T}.GetField(int)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyMemory<T> GetField(int index)
    {
        if (!_state.TryGetAtIndex(index, out ReadOnlyMemory<T> column))
        {
            ThrowIndexException(index);
        }

        return column;
    }

    /// <inheritdoc cref="ICsvRecord{T}.GetFieldCount"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetFieldCount() => _state.GetFieldCount();

    public bool TryGetValue<TValue>(int index, [MaybeNullWhen(false)] out TValue value) => TryGetValue(index, out value, out _);

    /// <inheritdoc cref="ICsvRecord{T}.TryGetValue{TValue}(int, out TValue, out CsvGetValueReason)"/>
    public bool TryGetValue<TValue>(
        int index,
        [MaybeNullWhen(false)] out TValue value,
        out CsvGetValueReason reason)
    {
        if (!_state.TryGetAtIndex(index, out ReadOnlyMemory<T> column))
        {
            reason = CsvGetValueReason.FieldNotFound;
            value = default;
            return false;
        }

        if (_options.TryGetParser<TValue>() is not { } parser)
        {
            reason = CsvGetValueReason.NoParserFound;
            value = default;
            return false;
        }

        if (!parser.TryParse(column.Span, out value))
        {
            reason = CsvGetValueReason.UnparsableValue;
            value = default;
            return false;
        }

        reason = CsvGetValueReason.Success;
        return true;
    }

    public bool TryGetValue<TValue>(string name, [MaybeNullWhen(false)] out TValue value) => TryGetValue(name, out value, out _);

    public bool TryGetValue<TValue>(string name, [MaybeNullWhen(false)] out TValue value, out CsvGetValueReason reason)
    {
        if (!_state.TryGetHeaderIndex(name, out int index))
        {
            value = default;
            reason = CsvGetValueReason.HeaderNotFound;
            return false;
        }

        return TryGetValue(index, out value, out reason);
    }

    public TValue GetField<TValue>(string name)
    {
        if (!_state.TryGetHeaderIndex(name, out int index))
        {
            ThrowHeaderException(name);
        }

        return GetField<TValue>(index);
    }

    /// <inheritdoc cref="ICsvRecord{T}.GetField{TValue}(int)"/>
    public TValue GetField<TValue>(int index)
    {
        if (!_state.TryGetAtIndex(index, out ReadOnlyMemory<T> column))
        {
            ThrowIndexException(index);
        }

        var parser = _options.GetParser<TValue>();

        if (!parser.TryParse(column.Span, out var value))
        {
            ThrowParseException(column.Span, typeof(TValue), parser);
        }

        return value;
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowHeaderException(string name)
    {
        Debug.Assert(_state._header is not null);

        string msg = _options.AllowContentInExceptions
            ? $"Header \"{name}\" was not found among the CSV headers: {string.Join(", ", _state._header.Keys)}"
            : "Header not found among the CSV headers.";

        throw new ArgumentException(msg, nameof(name));
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowIndexException(int index)
    {
        string? knownColumn;

        try
        {
            knownColumn = $" (there were {_state.GetFieldCount()} columns in the record)";
        }
        catch
        {
            knownColumn = null;
        }

        throw new ArgumentOutOfRangeException(
            nameof(index),
            $"Could not get column at index {index}{knownColumn}.");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowParseException(ReadOnlySpan<T> data, Type parsedType, object parser)
    {
        throw new CsvParseException(
            $"Failed to parse {parsedType.FullName} using {parser.GetType().FullName} " +
            $"from {data.AsPrintableString(_options.AllowContentInExceptions, _state.Dialect)}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CsvFieldEnumerator<T> GetEnumerator() => new(_state);

    IEnumerator<ReadOnlyMemory<T>> IEnumerable<ReadOnlyMemory<T>>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private sealed class CsvRecordDebugView
    {
        private readonly CsvRecord<T> _record;

        public CsvRecordDebugView(CsvRecord<T> record) => _record = record;

        public int Line => _record.Line;

        public long Position => _record.Position;

        public string[] Headers => _record._state._header?.Keys.ToArray() ?? Array.Empty<string>();

        public string[] Fields => _record._options is ICsvStringConfiguration<T> cfg
            ? _record.AsEnumerable().Select(f => cfg.GetTokensAsString(f.Span)).ToArray()
            : Array.Empty<string>();
    }
}
