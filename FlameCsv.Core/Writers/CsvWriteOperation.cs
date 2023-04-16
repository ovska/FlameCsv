using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Configuration;
using FlameCsv.Extensions;
using FlameCsv.Formatters;
using FlameCsv.Formatters.Internal;
using FlameCsv.Reading;

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
    /// <summary>
    /// Observed exception when reading the data.
    /// </summary>
    public Exception? Exception { get; set; }

    private readonly TWriter _writer;
    private readonly CsvDialect<T> _dialect;
    private readonly ArrayPool<T> _arrayPool;
    private readonly CsvFieldQuoting _fieldQuoting;
    private readonly ICsvNullTokenConfiguration<T>? _nullCfg;
    private T[]? _array;

    public CsvWriteOperation(
        TWriter writer,
        CsvWriterOptions<T> options)
    {
        _writer = writer;
        _dialect = new CsvDialect<T>(options);
        _arrayPool = options.ArrayPool ?? AllocatingArrayPool<T>.Instance;
        _fieldQuoting = options.FieldQuoting;
        _nullCfg = options as ICsvNullTokenConfiguration<T>;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask WriteValueAsync<TValue>(
        ICsvFormatter<T, TValue> formatter,
        TValue value,
        CancellationToken cancellationToken)
    {
        return !cancellationToken.IsCancellationRequested
            ? WriteValueCore(_writer.GetSpan(), formatter, value, cancellationToken)
            : ValueTask.FromCanceled(cancellationToken);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private ValueTask WriteValueCore<TValue>(
        Span<T> destination,
        ICsvFormatter<T, TValue> formatter,
        TValue value,
        CancellationToken cancellationToken)
    {
        bool formatSuccessful;
        int tokensWritten;

        // this whole branch is JITed out for value types
        if (value is null && !formatter.HandleNull)
        {
            formatSuccessful = _nullCfg.GetNullTokenOrDefault(typeof(TValue)).Span.TryWriteTo(destination, out tokensWritten);
        }
        else
        {
            formatSuccessful = formatter.TryFormat(value, destination, out tokensWritten);
        }

        if (!formatSuccessful)
        {
            // Buffer too small, grow and retry
            return GrowAndRetryAsync(destination.Length, formatter, value, cancellationToken);
        }

        // validate negative or too large tokensWritten in case of broken user-defined formatters
        if ((uint)tokensWritten > (uint)destination.Length)
        {
            WriteOpHelpers.ThrowForInvalidTokensWritten(formatter, tokensWritten, destination.Length);
        }

        // early exit for empty writes, e.g. nulls
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

            return default;
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
                    ReadOnlyMemory<T> overflow = WriteUtil<T>.EscapeWithOverflow(
                        written,
                        destination,
                        _dialect.Quote,
                        quoteCount,
                        new ValueBufferOwner<T>(ref _array, _arrayPool));

                    // The whole of the span is filled, with the leftovers being written to the overflow
                    _writer.Advance(destination.Length);

                    // Return the async version from the get go; this is a rare case and we can safely
                    // assume that pipe/textwriter needs to be flushed anyway if the buffer is full
                    return FlushAndWriteMemoryAsync(overflow, cancellationToken);
                }

                // escape directly to the destination buffer and adjust the tokens written accordingly
                WriteUtil<T>.Escape(written, destination[..escapedLength], _dialect.Quote, quoteCount);
                tokensWritten = escapedLength;
            }
        }

        _writer.Advance(tokensWritten);
        return default;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async ValueTask GrowAndRetryAsync<TValue>(
        int previousBufferLength,
        ICsvFormatter<T, TValue> formatter,
        TValue value,
        CancellationToken cancellationToken)
    {
        await _writer.FlushAsync(cancellationToken);
        await WriteValueCore(_writer.GetSpan(previousBufferLength * 2), formatter, value, cancellationToken);
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async ValueTask FlushAndWriteMemoryAsync(ReadOnlyMemory<T> tokens, CancellationToken cancellationToken)
    {
        await _writer.FlushAsync(cancellationToken);
        tokens.Span.CopyTo(_writer.GetSpan(tokens.Length));
        _writer.Advance(tokens.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask DisposeAsync()
    {
        _arrayPool.EnsureReturned(ref _array);
        return _writer.CompleteAsync(Exception);
    }
}
