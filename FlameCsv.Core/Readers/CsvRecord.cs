using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Readers.Internal;

namespace FlameCsv.Readers;

// todo: public api description
public struct CsvRecord<T> : IEnumerable<ReadOnlyMemory<T>>, IEnumerator<ReadOnlyMemory<T>>
    where T : unmanaged, IEquatable<T>
{
    private readonly int? _columnCount;
    private readonly BufferOwner<T> _bufferOwner;
    private readonly CsvReaderOptions<T> _options;

    private ReadOnlyMemory<T> _remaining;
    private int _quotesRemaining;

    /// <summary>
    /// The complete unescaped data on the line without trailing newline tokens.
    /// </summary>
    public ReadOnlyMemory<T> Line { get; }

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
        ReadOnlyMemory<T> line,
        CsvReaderOptions<T> options,
        int? columnCount,
        int? quoteCount,
        BufferOwner<T> bufferOwner)
    {
        _columnCount = columnCount;
        _bufferOwner = bufferOwner;

        _remaining = line;
        _options = options;
        _quotesRemaining = quoteCount ?? line.Span.Count(options.tokens.StringDelimiter);

        Line = line;
        Column = 0;
        Current = default;
    }

    public CsvRecord<T> GetEnumerator() => this;

    public TValue GetValue<TValue>(int index)
    {
        Guard.IsGreaterThanOrEqualTo(index, 0);
        Reset();

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
        var index = _quotesRemaining == 0
            ? _remaining.Span.IndexOf(_options.tokens.Delimiter)
            : _remaining.Span.IndexOfAny(_options.tokens.Delimiter, _options.tokens.StringDelimiter);

        while (index >= 0)
        {
            // Hit a comma, either found end of column or more columns than expected
            if (_remaining.Span[index].Equals(_options.tokens.Delimiter))
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

            var nextIndex = --_quotesRemaining == 0
                ? _remaining.Slice(index).Span.IndexOf(_options.tokens.Delimiter)
                : _remaining.Slice(index).Span.IndexOfAny(_options.tokens.Delimiter, _options.tokens.StringDelimiter);

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
                : data.Unescape(_options.tokens.StringDelimiter, quotesConsumed, _bufferOwner);

        return quotesConsumed == 0
            ? data.Trim(_options.tokens.Whitespace.Span)
            : data.Trim(_options.tokens.Whitespace.Span)
                .Unescape(_options.tokens.StringDelimiter, quotesConsumed, _bufferOwner);
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

    void IDisposable.Dispose()
    {
    }

    public void Reset()
    {
        _remaining = Line;
        _quotesRemaining = Line.Span.Count(_options.tokens.StringDelimiter);
        Column = 0;
        Current = default;
    }

    object IEnumerator.Current => Current;
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    IEnumerator<ReadOnlyMemory<T>> IEnumerable<ReadOnlyMemory<T>>.GetEnumerator() => GetEnumerator();
}
