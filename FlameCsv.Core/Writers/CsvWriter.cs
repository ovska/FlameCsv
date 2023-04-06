using System.Buffers;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using FlameCsv.Formatters;
using FlameCsv.Reading;

namespace FlameCsv.Writers;

internal sealed class CsvWriter<T> : IAsyncDisposable where T : unmanaged, IEquatable<T>
{
    public Exception? Exception { get; set; }

    private readonly ICsvPipeWriter<T> _pipe;
    private readonly CsvDialect<T> _dialect;
    private readonly ArrayPool<T> _arrayPool;
    private T[]? _array;

    public CsvWriter(
        ICsvPipeWriter<T> pipe,
        in CsvDialect<T> dialect,
        ArrayPool<T>? arrayPool)
    {
        _pipe = pipe;
        _dialect = dialect;
        _arrayPool = arrayPool ?? AllocatingArrayPool<T>.Instance;
    }

    public async ValueTask WriteValueAsync<TValue>(
        ICsvFormatter<T, TValue> formatter,
        TValue value,
        CancellationToken cancellationToken = default)
    {
        int tokensWritten;
        Memory<T> destination = _pipe.GetBuffer();

        while (!formatter.TryFormat(value, destination.Span, out tokensWritten))
        {
            destination = await _pipe.GrowAsync(cancellationToken);
        }

        if (tokensWritten == 0)
            return;

        Memory<T> written = destination[..tokensWritten];

        if (WriteUtil<T>.NeedsEscaping(written.Span, in _dialect, out int quoteCount))
        {
            int escapedLength = tokensWritten + 2 + quoteCount;

            if (destination.Length > escapedLength)
            {
                await EscapePartialValueAndAdvance(written, destination, quoteCount, cancellationToken);
                return;
            }
            else
            {
                WriteUtil<T>.Escape(written.Span, destination.Span[..escapedLength], _dialect.Quote, quoteCount);
                tokensWritten = escapedLength;
            }

        }

        _pipe.Advance(tokensWritten);
    }

    private async ValueTask EscapePartialValueAndAdvance(
        ReadOnlyMemory<T> written,
        Memory<T> destination,
        int quoteCount,
        CancellationToken cancellationToken)
    {
        ReadOnlyMemory<T> overflow = WriteUtil<T>.PartialEscape(
            written.Span,
            destination.Span,
            _dialect.Quote,
            quoteCount,
            new ValueBufferOwner<T>(ref _array, _arrayPool));

        _pipe.Advance(destination.Length);

        do
        {
            destination = await _pipe.GrowAsync(cancellationToken);
        } while (destination.Length < overflow.Length);

        overflow.Span.CopyTo(destination.Span);
        _pipe.Advance(overflow.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask WriteDelimiterAsync(CancellationToken cancellationToken)
    {
        Memory<T> destination = _pipe.GetBuffer();

        if (!destination.IsEmpty)
        {
            destination.Span[0] = _dialect.Delimiter;
            _pipe.Advance(1);
            return default;
        }

        return Core(cancellationToken);

        [MethodImpl(MethodImplOptions.NoInlining)]
        async ValueTask Core(CancellationToken cancellationToken)
        {
            Memory<T> destination;

            do
            {
                destination = await _pipe.GrowAsync(cancellationToken);
            } while (destination.IsEmpty);

            destination.Span[0] = _dialect.Delimiter;
            _pipe.Advance(1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask WriteNewlineAsync(CancellationToken cancellationToken)
    {
        Memory<T> destination = _pipe.GetBuffer();

        if (destination.Length >= _dialect.Newline.Length)
        {
            _dialect.Newline.Span.CopyTo(destination.Span);
            _pipe.Advance(_dialect.Newline.Length);
            return default;
        }

        return Core(cancellationToken);

        [MethodImpl(MethodImplOptions.NoInlining)]
        async ValueTask Core(CancellationToken cancellationToken)
        {
            Memory<T> destination;

            do
            {
                destination = await _pipe.GrowAsync(cancellationToken);
            } while (destination.Length < _dialect.Newline.Length);

            _dialect.Newline.Span.CopyTo(destination.Span);
            _pipe.Advance(_dialect.Newline.Length);
        }
    }

    public ValueTask DisposeAsync()
    {
        _arrayPool.EnsureReturned(ref _array);
        return _pipe.CompleteAsync(Exception);
    }
}
