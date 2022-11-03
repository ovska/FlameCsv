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
#if DEBUG
[DebuggerDisplay(
    @"\{ CsvColumnEnumerator: Column {Column}/{_columnCount?.ToString() ?? ""?""}, "
    + @"Remaining: {_remaining.Length}, Quotes remaining: {_quotesRemaining}, Current: [{Current}] \}")]
#endif
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
    public readonly bool IsKnownLastColumn
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Column + 1 == _columnCount.GetValueOrDefault();
    }

    /// <summary>
    /// Returns true if column count is known and all columns have been read,
    /// or column count is now known an all data has been read.
    /// </summary>
    public readonly bool IsAtEnd
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _columnCount.HasValue ? Column >= _columnCount.GetValueOrDefault() : _remaining.IsEmpty;
    }

    /// <summary>
    /// Initializes a column enumerator over a line.
    /// </summary>
    /// <param name="line">The line without trailing newline tokens</param>
    /// <param name="tokens">Structural tokens</param>
    /// <param name="columnCount">Amount of columns expected, null if not known</param>
    /// <param name="quoteCount">Known string delimiter count on the line</param>
    /// <param name="bufferOwner">Provides the buffer needed to unescape possible quotes insides strings</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvColumnEnumerator(
        ReadOnlySpan<T> line,
        in CsvTokens<T> tokens,
        int? columnCount,
        int quoteCount,
        BufferOwner<T> bufferOwner)
    {
        Debug.Assert(!line.IsEmpty || columnCount is null or 1, "Empty line is not valid for over 1 column");
        Debug.Assert(columnCount is null or > 0, "Known column count must be positive");
        Debug.Assert(quoteCount >= 0, "Quote count must be positive");
        Debug.Assert(quoteCount % 2 == 0, "Quote count must be divisible by 2");
        Debug.Assert(!tokens.TryGetValidationErrors(out _), "CsvTokens must be valid");
        Debug.Assert(bufferOwner is not null, "BufferOwner must not be null");

        _comma = tokens.Delimiter;
        _quote = tokens.StringDelimiter;
        _whitespace = tokens.Whitespace.Span;
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
        if (IsAtEnd)
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
                ? _remaining.Slice(index).IndexOf(_comma)
                : _remaining.Slice(index).IndexOfAny(_comma, _quote);

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

    /// <summary>
    /// Trims and unescapes the data according to <paramref name="quotesConsumed"/> and
    /// <see cref="_whitespace"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly ReadOnlySpan<T> TrimAndUnescape(ReadOnlySpan<T> data, int quotesConsumed)
    {
        if (_whitespace.IsEmpty)
            return quotesConsumed == 0
                ? data
                : data.Unescape(_quote, quotesConsumed, _bufferOwner);

        return quotesConsumed == 0
            ? data.Trim(_whitespace)
            : data.Trim(_whitespace).Unescape(_quote, quotesConsumed, _bufferOwner);
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
}
