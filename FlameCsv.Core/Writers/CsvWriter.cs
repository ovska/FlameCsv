using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using FlameCsv.Formatters;
using FlameCsv.Reading;

namespace FlameCsv.Writers;

internal static class CsvWriter
{
    public static CsvWriter<char> Create(TextWriter textWriter, CsvWriterOptions<char> options)
    {
        return new CsvWriter<char>(
            new CsvTextPipe(textWriter, options.ArrayPool),
            options.Dialect,
            options.ArrayPool);
    }

    public static CsvWriter<byte> Create(PipeWriter pipeWriter, CsvWriterOptions<byte> options)
    {
        return new CsvWriter<byte>(
            new CsvBytePipe(pipeWriter),
            options.Dialect,
            options.ArrayPool);
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
    private T[]? _array;

    public CsvWriter(
        ICsvPipe<T> pipe,
        in CsvDialect<T> dialect,
        ArrayPool<T>? arrayPool)
    {
        _pipe = pipe;
        _dialect = dialect;
        _arrayPool = arrayPool ?? AllocatingArrayPool<T>.Instance;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public ValueTask WriteValueAsync<TValue>(
        ICsvFormatter<T, TValue> formatter,
        TValue value,
        CancellationToken cancellationToken = default)
    {
        Span<T> destination = _pipe.GetSpan();

        if (!formatter.TryFormat(value, destination, out int tokensWritten))
        {
            // Buffer too small, grow and retry
            return GrowAndRetry(destination.Length);
        }

        // Format successful, check if we need to escape the value
        if (tokensWritten != 0)
        {
            Span<T> written = destination[..tokensWritten];

            if (WriteUtil<T>.NeedsEscaping(written, in _dialect, out int quoteCount))
            {
                int escapedLength = tokensWritten + 2 + quoteCount;

                // If there isn't enough space to escape, escape partially to the overflow buffer
                // to avoid having to call the formatter again after growing the buffer
                if (destination.Length > escapedLength)
                {
                    ReadOnlyMemory<T> overflow = WriteUtil<T>.PartialEscape(
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

            _pipe.Advance(tokensWritten);
        }

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
            Memory<T> destination = default;

            do
            {
                await _pipe.GrowAsync(destination.Length, cancellationToken);
                destination = _pipe.GetMemory();
            } while (destination.IsEmpty);

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

        if (destination.Length >= tokens.Length)
        {
            tokens.Span.CopyTo(destination);
            _pipe.Advance(tokens.Length);
            return default;
        }

        return Core();

        [MethodImpl(MethodImplOptions.NoInlining)]
        async ValueTask Core()
        {
            Memory<T> destination = default;

            do
            {
                await _pipe.GrowAsync(destination.Length, cancellationToken);
                destination = _pipe.GetMemory();
            } while (destination.Length < tokens.Length);

            tokens.Span.CopyTo(destination.Span);
            _pipe.Advance(tokens.Length);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _arrayPool.EnsureReturned(ref _array);
        await _pipe.CompleteAsync(Exception);
    }
}
