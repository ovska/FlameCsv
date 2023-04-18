using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;

namespace FlameCsv;

internal sealed class CsvEnumerationState<T> : IDisposable where T : unmanaged, IEquatable<T>
{
    public int Version { get; private set; }
    public CsvDialect<T> Dialect { get; }

    private readonly ArrayPool<T> _arrayPool;
    private readonly bool _exposeContents;
    private T[]? _unescapeBuffer;
    private Memory<T> _remainingUnescapeBuffer;

    private ReadOnlyMemory<T>[] _values;
    private int _index;

    private ReadOnlyMemory<T> _record;
    private ReadOnlyMemory<T> _remaining;
    private int _quotesRemaining;

    public CsvEnumerationState(CsvReaderOptions<T> options) : this(new CsvDialect<T>(options), options.ArrayPool)
    {
        _exposeContents = options.AllowContentInExceptions;
    }

    public CsvEnumerationState(CsvDialect<T> dialect, ArrayPool<T>? arrayPool)
    {
        Dialect = dialect;
        _arrayPool = arrayPool ?? AllocatingArrayPool<T>.Instance;
        _values = new ReadOnlyMemory<T>[32];
    }

    public void Initialize(ReadOnlyMemory<T> memory, int quoteCount)
    {
        Version++;
        _index = 0;
        _values.AsSpan().Clear();
        _record = memory;
        _remaining = memory;
        _quotesRemaining = quoteCount;

        if (quoteCount > 0)
            _arrayPool.EnsureCapacity(ref _unescapeBuffer, memory.Length - quoteCount / 2);

        _remainingUnescapeBuffer = _unescapeBuffer;
    }

    public void Dispose()
    {
        Version = -1;
        _index = int.MinValue;
        _values = Array.Empty<ReadOnlyMemory<T>>();
        _record = default;
        _remaining = default;
        _quotesRemaining = 0;
        _arrayPool.EnsureReturned(ref _unescapeBuffer);
        _remainingUnescapeBuffer = default;
    }

    public bool TryGetAtIndex(int index, out ReadOnlyMemory<T> column)
    {
        Guard.IsGreaterThanOrEqualTo(index, 0);

        while (_index <= index)
        {
            if (!TryReadNextColumn())
            {
                column = default;
                return false;
            }
        }

        column = _values[index];
        return true;
    }

    public int GetFieldCount()
    {
        while (TryReadNextColumn())
        { }

        return _index;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool TryReadNextColumn()
    {
        if (_remaining.IsEmpty)
            return false;

        // Since column count is not known in advance, we leave the dangling delimiter after each column
        // so we can differentiate between an empty last column and end of the data in general.
        if (_index != 0)
        {
            if (!_remaining.Span[0].Equals(Dialect.Delimiter))
                ThrowUnreachableInvalidState();

            _remaining = _remaining.Slice(1);
        }

        // keep track of how many quotes the current column has
        int quotesConsumed = 0;

        // If the remaining row has no quotes seek the next comma directly
        ReadOnlySpan<T> remaining = _remaining.Span;
        int index = _quotesRemaining == 0
            ? remaining.IndexOf(Dialect.Delimiter)
            : remaining.IndexOfAny(Dialect.Delimiter, Dialect.Quote);

        while (index >= 0)
        {
            // Hit a comma, either found end of column or more columns than expected
            if (remaining[index].Equals(Dialect.Delimiter))
            {
                AdvanceColumn(_remaining.Slice(0, index), quotesConsumed);
                _remaining = _remaining.Slice(index); // note: leave the comma in
                return true;
            }

            // Token found but was not delimiter, must be a quote. This branch is never taken if quotesRemaining is 0
            quotesConsumed++;
            index++; // move index past the quote

            int nextIndex = --_quotesRemaining == 0
                ? remaining.Slice(index).IndexOf(Dialect.Delimiter)
                : quotesConsumed % 2 == 0 // uneven quotes, only need to find the next one
                    ? remaining.Slice(index).IndexOfAny(Dialect.Delimiter, Dialect.Quote)
                    : remaining.Slice(index).IndexOf(Dialect.Quote);

            if (nextIndex < 0)
                break;

            index += nextIndex;
        }

        // No comma in the remaining data
        if (_quotesRemaining == 0)
        {
            AdvanceColumn(_remaining, quotesConsumed);
            _remaining = default; // consume all data
            return true;
        }

        // there were leftover unprocessed quotes
        return ThrowInvalidEOF();
    }

    private void AdvanceColumn(ReadOnlyMemory<T> unescapedColumn, int quotesConsumed)
    {
        if (_index >= _values.Length)
            Array.Resize(ref _values, _values.Length * 2);

        _values[_index++] = unescapedColumn.Unescape(Dialect.Quote, quotesConsumed, ref _remainingUnescapeBuffer);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowUnreachableInvalidState()
    {
        throw new UnreachableException(
            "The CSV record was in an invalid state (no delimiter at head after first column), " +
            $"Remaining: {_remaining.Span.AsPrintableString(_exposeContents, Dialect)}, " +
            $"Record: {_record.Span.AsPrintableString(_exposeContents, Dialect)}");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private bool ThrowInvalidEOF()
    {
        throw new UnreachableException(
            $"There were leftover quotes ({_quotesRemaining}) in the line: " +
            _record.Span.AsPrintableString(_exposeContents, Dialect));
    }
}

