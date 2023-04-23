using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;
using FlameCsv.Formatters;

namespace FlameCsv.Writers;

internal static class WriteOpHelpers
{
    public static CsvWriteOperation<char, CsvCharBufferWriter> Create(TextWriter textWriter, CsvWriterOptions<char> options)
    {
        return new CsvWriteOperation<char, CsvCharBufferWriter>(
            new CsvCharBufferWriter(textWriter, options.ArrayPool),
            options);
    }

    public static CsvWriteOperation<byte, CsvByteBufferWriter> Create(PipeWriter pipeWriter, CsvWriterOptions<byte> options)
    {
        return new CsvWriteOperation<byte, CsvByteBufferWriter>(
            new CsvByteBufferWriter(pipeWriter),
            options);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowForInvalidTokensWritten(object formatter, int tokensWritten, int destinationLength)
    {
        throw new InvalidOperationException(
            $"Formatter ({formatter.GetType().ToTypeString()}) reported {tokensWritten} " +
            $"tokens written to a buffer of length {destinationLength}.");
    }
}

internal sealed class CsvWriteOperation<T, TWriter> : IAsyncDisposable
    where T : unmanaged, IEquatable<T>
    where TWriter : struct, IAsyncBufferWriter<T>
{
    public bool NeedsFlush
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _writer.NeedsFlush;
    }

    /// <summary>
    /// Observed exception when reading the data.
    /// </summary>
    public Exception? Exception { get; set; }

    private readonly TWriter _writer;
    private readonly CsvDialect<T> _dialect;
    private readonly ArrayPool<T> _arrayPool;
    private readonly CsvFieldQuoting _fieldQuoting;
    private readonly CsvWriterOptions<T> _options;
    private T[]? _array;

    public CsvWriteOperation(
        TWriter writer,
        CsvWriterOptions<T> options)
    {
        _writer = writer;
        _dialect = new CsvDialect<T>(options);
        _arrayPool = options.ArrayPool.AllocatingIfNull();
        _fieldQuoting = options.FieldQuoting;
        _options = options;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void WriteValue<TValue>(ICsvFormatter<T, TValue> formatter, TValue value)
    {
        int tokensWritten;
        scoped Span<T> destination;

        // this whole branch is JITed out for value types
        if (value is null && !formatter.HandleNull)
        {
            ReadOnlySpan<T> nullValue = _options.GetNullToken(typeof(TValue)).Span;
            destination = _writer.GetSpan(nullValue.Length);
            nullValue.CopyTo(destination);
            tokensWritten = nullValue.Length;
        }
        else
        {
            destination = _writer.GetSpan();

            while (!formatter.TryFormat(value, destination, out tokensWritten))
            {
                destination = _writer.GetSpan(destination.Length * 2);
            }
        }

        // validate negative or too large tokensWritten in case of broken user-defined formatters
        if ((uint)tokensWritten > (uint)destination.Length)
        {
            WriteOpHelpers.ThrowForInvalidTokensWritten(formatter, tokensWritten, destination.Length);
        }

        // early exit for empty writes, like nulls or empty strings
        if (tokensWritten == 0)
        {
            if (_fieldQuoting == CsvFieldQuoting.Always)
            {
                // Ensure the buffer is large enough
                if (destination.Length < 2)
                    destination = _writer.GetSpan(2);

                stackalloc T[] { _dialect.Quote, _dialect.Quote }.CopyTo(destination);
                _writer.Advance(2);
            }

            return;
        }

        // Value formatted, check if it needs to be wrapped in quotes
        if (_fieldQuoting != CsvFieldQuoting.Never)
        {
            Span<T> written = destination[..tokensWritten];

            bool shouldQuote;
            int quoteCount;

            if (_fieldQuoting == CsvFieldQuoting.Always)
            {
                shouldQuote = true;
                quoteCount = written.Count(_dialect.Quote);
            }
            else
            {
                shouldQuote = WriteUtil<T>.NeedsQuoting(written, in _dialect, out quoteCount);
            }

            // if needed, escape/quote the field and adjust tokensWritten
            if (shouldQuote)
            {
                int escapedLength = tokensWritten + 2 + quoteCount;

                // If there isn't enough space to escape, escape partially to the overflow buffer
                // to avoid having to call the formatter again after growing the buffer
                if (escapedLength > destination.Length)
                {
                    ReadOnlySpan<T> overflow = WriteUtil<T>.EscapeWithOverflow(
                        source: written,
                        destination: destination,
                        quote: _dialect.Quote,
                        quoteCount: quoteCount,
                        overflowBuffer: ref _array,
                        arrayPool: _arrayPool);

                    // The whole of the span is filled, with the leftovers being written to the overflow
                    _writer.Advance(destination.Length);

                    overflow.CopyTo(_writer.GetSpan(overflow.Length));
                    _writer.Advance(overflow.Length);

                    return;
                }

                // escape directly to the destination buffer and adjust the tokens written accordingly
                WriteUtil<T>.Escape(written, destination[..escapedLength], _dialect.Quote, quoteCount);
                tokensWritten = escapedLength;
            }
        }

        _writer.Advance(tokensWritten);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDelimiter()
    {
        Span<T> destination = _writer.GetSpan(1);
        destination[0] = _dialect.Delimiter;
        _writer.Advance(1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteNewline()
    {
        Span<T> destination = _writer.GetSpan(_dialect.Newline.Length);
        _dialect.Newline.Span.CopyTo(destination);
        _writer.Advance(_dialect.Newline.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteString(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
            return;

        if (typeof(T) == typeof(char))
        {
            Span<T> destination = _writer.GetSpan(value.Length);
            value.CopyTo(destination.Cast<T, char>());
            _writer.Advance(value.Length);
        }
        else if (typeof(T) == typeof(byte))
        {
            Span<T> destination = _writer.GetSpan(Encoding.UTF8.GetMaxByteCount(value.Length));
            int written = Encoding.UTF8.GetBytes(value, destination.Cast<T, byte>());
            _writer.Advance(written);
        }
        else
        {
            Token<T>.ThrowNotSupportedException();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        return !cancellationToken.IsCancellationRequested
            ? _writer.FlushAsync(cancellationToken)
            : ValueTask.FromCanceled(cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask DisposeAsync()
    {
        _arrayPool.EnsureReturned(ref _array);
        return _writer.CompleteAsync(Exception);
    }
}
