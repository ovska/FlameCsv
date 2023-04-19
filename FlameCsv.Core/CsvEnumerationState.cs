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
    public bool NeedsHeader
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _hasHeader && _header is null;
    }

    public int Version { get; private set; }
    public CsvDialect<T> Dialect { get; }

    private readonly bool _exposeContents; // allow content in exceptions

    private readonly ArrayPool<T> _arrayPool; // pool used for unescape buffer
    private T[]? _unescapeBuffer; // rented array for unescaping
    private Memory<T> _remainingUnescapeBuffer; // unused tail of the buffer

    private ReadOnlyMemory<T>[] _values; // cached column values by index
    private int _index; // how many values have been read

    private ReadOnlyMemory<T> _record; // whole record's raw data
    private ReadOnlyMemory<T> _remaining; // unread data, if empty, the whole record has been read
    private int _quotesRemaining; // how many quotes are left in the remaining data

    private readonly bool _hasHeader;
    internal Dictionary<string, int>? _header;

    public CsvEnumerationState(CsvReaderOptions<T> options) : this(new CsvDialect<T>(options), options.ArrayPool)
    {
        _hasHeader = options.HasHeader;
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

    public bool TryGetHeaderIndex(string name, out int index)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (!_hasHeader)
            ThrowHelper.ThrowNotSupportedException("The current CSV does not have a header record.");

        if (_header is null)
            ThrowHelper.ThrowInvalidOperationException("CSV header has not been read.");

        return _header.TryGetValue(name, out index);
    }

    public bool TryGetAtIndex(int index, out ReadOnlyMemory<T> column)
    {
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

    public void SetHeader(Dictionary<string, int> header)
    {
        if (!_hasHeader)
            ThrowHelper.ThrowNotSupportedException("The current CSV does not have a header record.");

        if (_header is not null)
            ThrowHelper.ThrowInvalidOperationException("CSV header has already been read.");

        _header = header;
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
                ThrowNoDelimiterAtHead();

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

        if (_quotesRemaining != 0)
        {
            // there were leftover unprocessed quotes
            ThrowInvalidEOF();
        }

        AdvanceColumn(_remaining, quotesConsumed);
        _remaining = default; // consume all data
        return true;
    }

    private void AdvanceColumn(ReadOnlyMemory<T> unescapedColumn, int quotesConsumed)
    {
        if (_index >= _values.Length)
            Array.Resize(ref _values, _values.Length * 2);

        _values[_index++] = unescapedColumn.Unescape(Dialect.Quote, quotesConsumed, ref _remainingUnescapeBuffer);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowNoDelimiterAtHead()
    {
        throw new UnreachableException(
            "The CSV record was in an invalid state (no delimiter at head after first column), " +
            $"Remaining: {_remaining.Span.AsPrintableString(_exposeContents, Dialect)}, " +
            $"Record: {_record.Span.AsPrintableString(_exposeContents, Dialect)}");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowInvalidEOF()
    {
        throw new UnreachableException(
            $"The CSV record was in an invalid state ({_quotesRemaining} leftover quotes), " +
            $"Record: {_record.Span.AsPrintableString(_exposeContents, Dialect)}");
    }
}

