using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Configuration;
using FlameCsv.Extensions;
using FlameCsv.Formatters;
using FlameCsv.Formatters.Internal;
using FlameCsv.Reading;

namespace FlameCsv.Writers;

internal static class CsvWriter
{
    public static CsvWriter<char> Create(TextWriter textWriter, CsvWriterOptions<char> options)
    {
        return new CsvWriter<char>(new CsvTextPipe(textWriter, options.ArrayPool), options);
    }

    public static CsvWriter<byte> Create(PipeWriter pipeWriter, CsvWriterOptions<byte> options)
    {
        return new CsvWriter<byte>(new CsvBytePipe(pipeWriter), options);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowForInvalidTokensWritten(int tokensWritten, int destinationLength)
    {
        throw new InvalidOperationException(
            $"Formatter reported {tokensWritten} tokens written to a buffer of length {destinationLength}.");
    }
}

internal sealed class CsvWriter<T> : IAsyncDisposable where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Observed exception when reading the data.
    /// </summary>
    public Exception? Exception { get; set; }

    private readonly ICsvPipe<T> _pipe;
    private readonly CsvDialect<T> _dialect;
    private readonly ArrayPool<T> _arrayPool;
    private readonly CsvFieldQuoting _fieldQuoting;
    private readonly ICsvNullTokenConfiguration<T>? _nullCfg;
    private T[]? _array;

    public CsvWriter(
        ICsvPipe<T> pipe,
        CsvWriterOptions<T> options)
    {
        _pipe = pipe;
        _dialect = new CsvDialect<T>(options);
        _arrayPool = options.ArrayPool ?? AllocatingArrayPool<T>.Instance;
        _fieldQuoting = options.FieldQuoting;
        _nullCfg = options as ICsvNullTokenConfiguration<T>;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public ValueTask WriteValueAsync<TValue>(
        ICsvFormatter<T, TValue> formatter,
        TValue value,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled(cancellationToken);
        }

        // this whole branch is JITed out for value types
        if (value is null && !formatter.HandleNull)
        {
            return WriteValueAsync(
                MemoryFormatter<T>.Instance,
                _nullCfg.GetNullTokenOrDefault(typeof(TValue)),
                cancellationToken);
        }

        Span<T> destination = _pipe.GetSpan();

        if (!formatter.TryFormat(value, destination, out int tokensWritten))
        {
            // Buffer too small, grow and retry
            return GrowAndRetry(destination.Length);
        }

        // validate tokensWritten in case of broken user-defined formatters
        // this check also handles negative values
        if ((uint)tokensWritten > (uint)destination.Length)
        {
            CsvWriter.ThrowForInvalidTokensWritten(tokensWritten, destination.Length);
        }

        // early exit for empty writes, e.g. nulls
        if (tokensWritten == 0)
        {
            return _fieldQuoting == CsvFieldQuoting.Always
                ? WriteEmptyQuotes(destination, cancellationToken)
                : default;
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

                    _pipe.Advance(destination.Length);

                    return WriteTokensAsync(overflow, cancellationToken);
                }

                // escape directly to the destination buffer and adjust the tokens written accordingly
                WriteUtil<T>.Escape(written, destination[..escapedLength], _dialect.Quote, quoteCount);
                tokensWritten = escapedLength;
            }
        }

        _pipe.Advance(tokensWritten);
        return default;

        [MethodImpl(MethodImplOptions.NoInlining)]
        async ValueTask GrowAndRetry(int previousBufferLength)
        {
            await _pipe.GrowAsync(previousBufferLength, cancellationToken);
            await WriteValueAsync(formatter, value, cancellationToken);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask WriteDelimiterAsync(CancellationToken cancellationToken)
    {
        Span<T> destination = _pipe.GetSpan();

        if (!destination.IsEmpty)
        {
            destination[0] = _dialect.Delimiter;
            _pipe.Advance(1);
            return default;
        }

        return Core();

        [MethodImpl(MethodImplOptions.NoInlining)]
        async ValueTask Core()
        {
            Memory<T> destination = await GrowToAtLeastAsync(1, cancellationToken);
            destination.Span[0] = _dialect.Delimiter;
            _pipe.Advance(1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask WriteNewlineAsync(CancellationToken cancellationToken)
    {
        return WriteTokensAsync(_dialect.Newline, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ValueTask WriteTokensAsync(ReadOnlyMemory<T> tokens, CancellationToken cancellationToken)
    {
        Span<T> destination = _pipe.GetSpan();

        if (tokens.Span.TryCopyTo(destination))
        {
            _pipe.Advance(tokens.Length);
            return default;
        }

        return Core();

        [MethodImpl(MethodImplOptions.NoInlining)]
        async ValueTask Core()
        {
            Memory<T> destination = await GrowToAtLeastAsync(tokens.Length, cancellationToken);
            tokens.Span.CopyTo(destination.Span);
            _pipe.Advance(tokens.Length);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ValueTask WriteEmptyQuotes(Span<T> destination, CancellationToken cancellationToken)
    {
        if (destination.Length >= 2)
        {
            destination[0] = _dialect.Quote;
            destination[1] = _dialect.Quote;
            _pipe.Advance(2);
            return default;
        }

        return Core();

        [MethodImpl(MethodImplOptions.NoInlining)]
        async ValueTask Core()
        {
            Memory<T> destination = await GrowToAtLeastAsync(2, cancellationToken);
            stackalloc T[] { _dialect.Quote, _dialect.Quote }.CopyTo(destination.Span);
            _pipe.Advance(2);
        }
    }

    private async ValueTask<Memory<T>> GrowToAtLeastAsync(int length, CancellationToken cancellationToken)
    {
        Memory<T> destination = default;

        do
        {
            await _pipe.GrowAsync(destination.Length, cancellationToken);
            destination = _pipe.GetMemory();
        } while (destination.Length < length);

        return destination;
    }

    public async ValueTask DisposeAsync()
    {
        _arrayPool.EnsureReturned(ref _array);
        await _pipe.CompleteAsync(Exception);
    }
}
