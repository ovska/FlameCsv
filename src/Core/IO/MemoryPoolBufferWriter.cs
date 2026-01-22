using FlameCsv.IO.Internal;

namespace FlameCsv.IO;

internal sealed class MemoryPoolBufferWriter<T> : CsvBufferWriter<T>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly Action<ReadOnlySpan<T>, bool>? _onFlush;
    private readonly Func<ReadOnlyMemory<T>, bool, CancellationToken, ValueTask>? _onFlushAsync;

    public MemoryPoolBufferWriter(
        Action<ReadOnlySpan<T>, bool>? onFlush,
        Func<ReadOnlyMemory<T>, bool, CancellationToken, ValueTask>? onFlushAsync,
        in CsvIOOptions ioOptions
    )
        : base(in ioOptions)
    {
        _onFlush = onFlush;
        _onFlushAsync = onFlushAsync;
    }

    protected override void DisposeCore() { }

    protected override ValueTask DisposeCoreAsync()
    {
        return default;
    }

    protected override ValueTask DrainAsyncCore(ReadOnlyMemory<T> memory, CancellationToken cancellationToken)
    {
        Check.NotNull(_onFlushAsync);
        return _onFlushAsync(memory, false, cancellationToken);
    }

    protected override void DrainCore(ReadOnlyMemory<T> memory)
    {
        Check.NotNull(_onFlush);
        _onFlush(memory.Span, false);
    }

    protected override void FlushCore()
    {
        _onFlush!([], true);
    }

    protected override ValueTask FlushAsyncCore(CancellationToken cancellationToken)
    {
        return _onFlushAsync!(ReadOnlyMemory<T>.Empty, true, CancellationToken.None);
    }
}
