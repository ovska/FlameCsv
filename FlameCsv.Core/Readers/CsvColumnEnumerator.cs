using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using FlameCsv.Readers.Internal;

namespace FlameCsv.Readers;

/// <summary>
/// Enumerates a columns from a line of data.
/// </summary>
/// <typeparam name="T"></typeparam>
internal ref struct CsvColumnEnumerator<T> where T : unmanaged, IEquatable<T>
{
    private readonly T _comma;
    private readonly T _quote;
    private readonly ReadOnlySpan<T> _whitespace;
    private readonly int? _columnCount;
    private readonly BufferOwner<T> _bufferOwner;

    private ReadOnlySpan<T> _remaining;
    private int _quotesRemaining;

    /// <summary>
    /// Current column without string delimiters.
    /// The value is also trimmed of whitespace if defined in options.
    /// </summary>
    public ReadOnlySpan<T> Current { get; private set; }

    /// <summary>
    /// Current column index.
    /// </summary>
    public int Column { get; private set; }

    /// <summary>
    /// Returns true if the previous <see cref="MoveNext"/> was the final expected column.
    /// Always returns false if column count is not known.
    /// </summary>
    public bool IsLastColumn
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Column + 1 == _columnCount.GetValueOrDefault();
    }

    /// <summary>
    /// Initializes a column enumerator over a line.
    /// </summary>
    /// <param name="line">The line without trailing newline tokens</param>
    /// <param name="options">Options instance</param>
    /// <param name="columnCount">Amount of columns expected, null if not known</param>
    /// <param name="quoteCount">Known string delimiter count on the line</param>
    /// <param name="bufferOwner">Provides the buffer needed to unescape possible quotes insides strings</param>
    internal CsvColumnEnumerator(
        ReadOnlySpan<T> line,
        in CsvParserOptions<T> options,
        int? columnCount,
        int quoteCount,
        BufferOwner<T> bufferOwner)
    {
        Debug.Assert(!line.IsEmpty || columnCount is null or 1);
        Debug.Assert(columnCount is null or > 0);
        Debug.Assert(quoteCount % 2 == 0 && quoteCount >= 0);
        Debug.Assert(!options.TryGetValidationErrors(out _));

        _comma = options.Delimiter;
        _quote = options.StringDelimiter;
        _whitespace = options.Whitespace.Span;
        _columnCount = columnCount;
        _bufferOwner = bufferOwner;

        _remaining = line;
        _quotesRemaining = quoteCount;

        Column = 0;
        Current = default;
    }

    public CsvColumnEnumerator<T> GetEnumerator() => this;

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool MoveNext()
    {
        if (_columnCount.HasValue ? Column >= _columnCount.GetValueOrDefault() : _remaining.IsEmpty)
            return false;

        // keep track of how many quotes the current column has
        var quotesConsumed = 0;

        // If the remaining row has no quotes seek the next comma directly
        var index = _quotesRemaining == 0
            ? _remaining.IndexOf(_comma)
            : _remaining.IndexOfAny(_comma, _quote);

        while (index >= 0)
        {
            // Hit a comma, either found end of column or more columns than expected
            if (_remaining[index].Equals(_comma))
            {
                if (IsLastColumn) ThrowTooManyColumns(index);

                Current = _remaining.Slice(0, index).Trim(_whitespace);

                // unescape if the column had quotes
                if (quotesConsumed > 0)
                {
                    Current = Current.Unescape(_quote, quotesConsumed, _bufferOwner);
                }

                // slice past the delimiter
                _remaining = _remaining.Slice(index + 1);
                Column++;
                return true;
            }

            // Token found but was not delimiter, must be a quote. This branch is never taken if quotesRemaining is 0
            quotesConsumed++;
            index++; // move index past the quote

            var nextIndex = --_quotesRemaining == 0
                ? _remaining.Slice(index).IndexOf(_comma)
                : _remaining.Slice(index).IndexOfAny(_comma, _quote);

            if (nextIndex < 0)
                break;

            index += nextIndex;
        }

        // No comma in the remaining data
        if ((IsLastColumn || !_columnCount.HasValue) && _quotesRemaining == 0)
        {
            Current = _remaining.Trim(_whitespace);

            if (quotesConsumed > 0)
            {
                Current = Current.Unescape(_quote, quotesConsumed, _bufferOwner);
            }

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
    public void EnsureAllColumnsRead()
    {
        if (_columnCount.HasValue ? Column == _columnCount.GetValueOrDefault() : _remaining.IsEmpty)
            return;

        ThrowNotAllColumnsRead();
    }

    /// <exception cref="InvalidDataException">
    /// Thrown when column count is known and there were too many commas
    /// </exception>
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowTooManyColumns(int index)
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
    private bool ThrowInvalidEOF()
    {
        throw new InvalidDataException(
            $"Line ended prematurely, expected {_columnCount} but read {Column} "
            + $"with {_quotesRemaining} string delimiters remaining.");
    }

    /// <exception cref="InvalidDataException">
    /// Thrown when all columns were not consumed as expected.
    /// </exception>
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowNotAllColumnsRead()
    {
        throw new InvalidDataException($"Expected {_columnCount} columns to have been read, but read {Column}");
    }
}
