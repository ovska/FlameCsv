using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Readers.Internal;

namespace FlameCsv.Readers;

public interface ICsvRecord<T> :
    IEnumerable<ReadOnlyMemory<T>>,
    IEnumerator<ReadOnlyMemory<T>>
    where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Token position at the start of <see cref="Data"/> in the CSV.
    /// </summary>
    long Position { get; }

    /// <summary>
    /// 1-based line index in the CSV.
    /// </summary>
    int Line { get; }

    /// <summary>
    /// The complete unescaped data on the line without trailing newline tokens.
    /// </summary>
    ReadOnlyMemory<T> Data { get; }

    /// <inheritdoc cref="CsvColumnEnumerator{T}.Column"/>
    public int Column { get; }

    /// <inheritdoc cref="CsvColumnEnumerator{T}.IsKnownLastColumn"/>
    public bool IsKnownLastColumn { get; }

    /// <inheritdoc cref="CsvColumnEnumerator{T}.IsAtEnd"/>
    public bool IsAtEnd { get; }
}

// todo: public api description
public struct CsvRecord<T> : ICsvRecord<T> where T : unmanaged, IEquatable<T>
{
    private readonly int? _columnCount;
    private readonly BufferOwner<T> _bufferOwner;
    private readonly CsvReaderOptions<T> _options;

    private ReadOnlyMemory<T> _remaining;
    private int _quotesRemaining;

    /// <summary>
    /// Token positionat the start of line in the source data.
    /// </summary>
    public long Position { get; }

    /// <summary>
    /// 1-based line index in the source data.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// The complete unescaped data on the line without trailing newline tokens.
    /// </summary>
    public ReadOnlyMemory<T> Data { get; }

    /// <inheritdoc cref="CsvColumnEnumerator{T}.Current"/>
    public ReadOnlyMemory<T> Current { get; private set; }

    /// <inheritdoc cref="CsvColumnEnumerator{T}.Column"/>
    public int Column { get; private set; }

    /// <inheritdoc cref="CsvColumnEnumerator{T}.IsKnownLastColumn"/>
    public readonly bool IsKnownLastColumn
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Column + 1 == _columnCount.GetValueOrDefault();
    }

    /// <inheritdoc cref="CsvColumnEnumerator{T}.IsAtEnd"/>
    public readonly bool IsAtEnd
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _columnCount.HasValue ? Column >= _columnCount.GetValueOrDefault() : _remaining.IsEmpty;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvRecord(
        ReadOnlyMemory<T> data,
        CsvReaderOptions<T> options,
        int? columnCount,
        int? quoteCount,
        BufferOwner<T> bufferOwner,
        long position,
        int line)
    {
        Position = position;
        Line = line;

        _columnCount = columnCount;
        _bufferOwner = bufferOwner;

        _remaining = data;
        _options = options;
        _quotesRemaining = quoteCount ?? data.Span.Count(options.tokens.StringDelimiter);

        Data = data;
        Column = 0;
        Current = default;
    }

    public readonly CsvRecord<T> GetEnumerator() => this;

    public TValue GetValue<TValue>(int index)
    {
        Guard.IsGreaterThanOrEqualTo(index, 0);
        Reset(); // TODO FIXME: cache parsed columns

        while (Column <= index)
        {
            if (!MoveNext())
                ThrowHelper.ThrowArgumentOutOfRangeException();
        }

        if (!_options.GetParser<TValue>().TryParse(Current.Span, out var value))
            throw new CsvParseException();

        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool MoveNext()
    {
        if (IsAtEnd)
            return false;

        // keep track of how many quotes the current column has
        var quotesConsumed = 0;

        // If the remaining row has no quotes seek the next comma directly
        var remaining = _remaining.Span;
        var index = _quotesRemaining == 0
            ? remaining.IndexOf(_options.tokens.Delimiter)
            : remaining.IndexOfAny(_options.tokens.Delimiter, _options.tokens.StringDelimiter);

        while (index >= 0)
        {
            // Hit a comma, either found end of column or more columns than expected
            if (remaining[index].Equals(_options.tokens.Delimiter))
            {
                if (IsKnownLastColumn) ThrowTooManyColumns(index);

                Current = TrimAndUnescape(_remaining.Slice(0, index), quotesConsumed);
                _remaining = _remaining.Slice(index + 1);
                Column++;
                return true;
            }

            // Token found but was not delimiter, must be a quote. This branch is never taken if quotesRemaining is 0
            quotesConsumed++;
            index++; // move index past the quote

            int nextIndex = --_quotesRemaining == 0
                ? _remaining.Span.Slice(index).IndexOf(_options.tokens.Delimiter)
                : quotesConsumed % 2 == 0 // uneven quotes, only need to find the next one
                    ? _remaining.Span.Slice(index).IndexOfAny(_options.tokens.Delimiter, _options.tokens.StringDelimiter)
                    : _remaining.Span.Slice(index).IndexOf(_options.tokens.StringDelimiter);

            if (nextIndex < 0)
                break;

            index += nextIndex;
        }

        // No comma in the remaining data
        if ((IsKnownLastColumn || !_columnCount.HasValue) && _quotesRemaining == 0)
        {
            Current = TrimAndUnescape(_remaining, quotesConsumed);
            _remaining = default;
            Column++;
            return true;
        }

        // Column count is known and there were too little columns OR
        // there were leftover unprocessed quotes (or the parameter quote count was invalid)
        return ThrowInvalidEOF();
    }

    /// <summary>
    /// Verifies that the previous <see cref="MoveNext"/> processed the final column, either by checking
    /// the known colun count or verifying all data was read.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void EnsureAllColumnsRead()
    {
        if (IsAtEnd)
            return;

        ThrowNotAllColumnsRead();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly ReadOnlyMemory<T> TrimAndUnescape(ReadOnlyMemory<T> data, int quotesConsumed)
    {
        if (_options.tokens.Whitespace.IsEmpty)
            return quotesConsumed == 0
                ? data
                : data.Unescape(_options.tokens.StringDelimiter, quotesConsumed, ref _bufferOwner._array);

        return quotesConsumed == 0
            ? data.Trim(_options.tokens.Whitespace.Span)
            : data.Trim(_options.tokens.Whitespace.Span)
                .Unescape(_options.tokens.StringDelimiter, quotesConsumed, ref _bufferOwner._array);
    }

    /// <exception cref="InvalidDataException">
    /// Thrown when column count is known and there were too many commas
    /// </exception>
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private readonly void ThrowTooManyColumns(int index)
    {
        throw new InvalidDataException(
            $"Too many columns read, expected {Column} to be the last but found delimiter "
            + $"at line index {_remaining.Length + index}");
    }

    /// <exception cref="InvalidDataException">
    /// Thrown when column count is known and there were too little commas, or
    /// there was an invalid amount of string delimiters.
    /// </exception>
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private readonly bool ThrowInvalidEOF()
    {
        throw new InvalidDataException(
            $"Line ended prematurely, expected {_columnCount} but read {Column} "
            + $"with {_quotesRemaining} string delimiters remaining.");
    }

    /// <exception cref="InvalidDataException">
    /// Thrown when all columns were not consumed as expected.
    /// </exception>
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private readonly void ThrowNotAllColumnsRead()
    {
        throw new InvalidDataException($"Expected {_columnCount} columns to have been read, but read {Column}");
    }

    readonly void IDisposable.Dispose()
    {
    }

    public void Reset()
    {
        _remaining = Data;
        _quotesRemaining = Data.Span.Count(_options.tokens.StringDelimiter);
        Column = 0;
        Current = default;
    }

    readonly object IEnumerator.Current => Current;
    readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    readonly IEnumerator<ReadOnlyMemory<T>> IEnumerable<ReadOnlyMemory<T>>.GetEnumerator() => GetEnumerator();
}
