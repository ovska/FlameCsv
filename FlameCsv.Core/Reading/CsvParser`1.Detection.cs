using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Utilities;

namespace FlameCsv.Reading;

public abstract partial class CsvParser<T>
{
    /// <summary>
    /// Attempt to auto-detect newline from the data.
    /// </summary>
    /// <returns>True if the current sequence contained a CRLF or LF (checked in that order)</returns>
    protected bool TryPeekNewline()
    {
        if (_newline.Length != 0)
        {
            Throw.Unreachable($"{nameof(TryPeekNewline)} called with newline length of {_newline.Length}");
        }

        if (_sequence.IsEmpty)
        {
            return false;
        }

        ref NewlineBuffer<T> destination = ref Unsafe.AsRef(in _newline);

        ReadOnlySpan<T> first = _sequence.FirstSpan;

        if (first.Length > MaxNewlineDetectionLength)
        {
            first = first.Slice(0, MaxNewlineDetectionLength);
        }

        // optimistic fast path for the first line containing no quotes or escapes
        if (first.IndexOf(NewlineBuffer<T>.LF.First) is var linefeedIndex and not -1)
        {
            // data starts with LF?
            if (linefeedIndex == 0)
            {
                destination = NewlineBuffer<T>.LF;
                return true;
            }

            // ensure there were no quotes or escapes between the start of the buffer and the linefeed
            ReadOnlySpan<T> untilLf = first.Slice(0, linefeedIndex);

            if (untilLf.IndexOfAny(_dialect.Quote, _dialect.Escape ?? _dialect.Quote) == -1)
            {
                // check if CR in the data
                int firstCR = untilLf.IndexOf(NewlineBuffer<T>.CRLF.First);

                if (firstCR == -1)
                {
                    destination = NewlineBuffer<T>.LF;
                    return true;
                }

                if (firstCR == untilLf.Length - 1)
                {
                    destination = NewlineBuffer<T>.CRLF;
                    return true;
                }
            }
        }

        // could not find a newline in the first segment, try to find it in the entire sequence
        ReadOnlySequence<T> copy = _sequence;

        // limit the amount of data we read to avoid reading the entire CSV
        if (_sequence.Length > MaxNewlineDetectionLength)
        {
            _sequence = _sequence.Slice(0, MaxNewlineDetectionLength);
        }

        NewlineBuffer<T> result = default;

        try
        {
            // find the first linefeed as both auto-detected newlines contain it
            destination = NewlineBuffer<T>.LF;

            while (TryReadFromSequence(out var firstLine, false))
            {
                // found a non-empty line?
                if (!firstLine.Data.IsEmpty)
                {
                    result = firstLine.Data.Span[^1] == NewlineBuffer<T>.CRLF.First
                        ? NewlineBuffer<T>.CRLF
                        : NewlineBuffer<T>.LF;
                    return true;
                }
            }

            // no line found, reset to the original state
            result = default;

            // \n not found, throw if we've read up to our threshold already
            if (copy.Length >= MaxNewlineDetectionLength)
            {
                throw new CsvFormatException(
                    $"Could not auto-detect newline even after {copy.Length} characters (no valid CRLF or LF tokens found)");
            }

            // maybe the first segment was just too small, or contained a single line without a newline
            return false;
        }
        finally
        {
            _sequence = copy; // reset original state
            destination = result; // set the detected newline, or default if not found
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void DetectDelimiter()
    {
        Debug.Assert(_delimiterDetectionStrategy is not null);

        if (_newline.Length == 0)
        {
            throw new UnreachableException(nameof(DetectDelimiter) + " called with empty newline length");
        }

        ReadOnlySpan<T> first = _sequence.FirstSpan;

        if (first.IsEmpty)
        {
            throw new CsvFormatException("Could not auto-detect delimiter from empty data.");
        }

        int fieldCount = Math.Min(16, _delimiterDetectionStrategy.RecordCountHint ?? 4);

        using var list = new ValueListBuilder<Range>(stackalloc Range[Math.Min(16, fieldCount)]);

        int index;
        int consumed = 0;

        if (_newline.Length == 2)
        {
            while (
                list.Length < fieldCount &&
                (index = first.Slice(consumed).IndexOf([_newline.First, _newline.Second])) >= 0)
            {
                list.Append(new Range(consumed, index));
                consumed += index + 2;
            }
        }
        else
        {
            while (
                list.Length < fieldCount &&
                (index = first.Slice(consumed).IndexOf(_newline.First)) >= 0)
            {
                list.Append(new Range(consumed, consumed + index));
                consumed += index + 1;
            }
        }

        // if we found no newlines, just add the entire first segment
        if (list.Length == 0)
        {
            if (!_readerCompleted)
            {
                if (_delimiterDetectionStrategy.IsOptional)
                {
                    _delimiterDetectionStrategy = null;
                    return;
                }

                throw new CsvFormatException(
                    $"Could not detect the delimiter using {_delimiterDetectionStrategy}; " +
                    $"the first {first.Length} {Token<T>.FriendlyName}s in the data did not contain any newlines.");
            }

            list.Append(new Range(0, first.Length));
        }

        if (_delimiterDetectionStrategy.TryDetect(first, list.AsSpan(), out T delimiter, out int consumedRecords))
        {
            try
            {
                if (delimiter != _dialect.Delimiter)
                {
                    CsvDialect<T> dialect = _dialect with { Delimiter = delimiter };
                    dialect.Validate();
                    Unsafe.AsRef(in _dialect) = dialect;
                }

                if (consumedRecords > 0)
                {
                    if (consumedRecords > list.Length)
                    {
                        Throw.InvalidOperation(
                            $"{_delimiterDetectionStrategy} reported {consumedRecords} records out of {list.Length}.");
                    }

                    _sequence = _sequence
                        .Slice(Math.Min(list[consumedRecords - 1].End.Value + _newline.Length, first.Length));
                }

                _delimiterDetectionStrategy = null;
                return;
            }
            catch (CsvConfigurationException cex)
            {
                throw new CsvFormatException(
                    $"Auto-detected delimiter {delimiter} caused the dialect to be in an invalid state",
                    cex);
            }
        }

        // failed to detect a delimiter, but it's optional
        if (_delimiterDetectionStrategy.IsOptional)
        {
            _delimiterDetectionStrategy = null;
            return;
        }

        throw new CsvFormatException(
            $"Could not auto-detect delimiter from the first {list.Length} line(s) in the data using strategy: {_delimiterDetectionStrategy}.");
    }
}
