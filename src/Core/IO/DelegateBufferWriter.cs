using FlameCsv.IO.Internal;

namespace FlameCsv.IO;

internal sealed class DelegateBufferWriter<T> : CsvBufferWriter<T>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly Csv.ParallelSink<T>? _sink;
    private readonly Csv.AsyncParallelSink<T>? _asyncSink;

    public DelegateBufferWriter(
        Csv.ParallelSink<T>? onFlush,
        Csv.AsyncParallelSink<T>? onFlushAsync,
        in CsvIOOptions ioOptions
    )
        : base(in ioOptions)
    {
        _sink = onFlush;
        _asyncSink = onFlushAsync;
    }

    protected override void DisposeCore() { }

    protected override ValueTask DisposeCoreAsync()
    {
        return default;
    }

    protected override void DrainCore(ReadOnlyMemory<T> memory)
    {
        Check.NotNull(_sink);
        _sink(memory.Span, isFinalWrite: false);
    }

    protected override void FlushCore()
    {
        Check.NotNull(_sink);
        _sink!([], isFinalWrite: true);
    }

    protected override ValueTask DrainAsyncCore(ReadOnlyMemory<T> memory, CancellationToken cancellationToken)
    {
        Check.NotNull(_asyncSink);
        return _asyncSink(memory, isFinalWrite: false, cancellationToken);
    }

    protected override ValueTask FlushAsyncCore(CancellationToken cancellationToken)
    {
        Check.NotNull(_asyncSink);
        return _asyncSink!(ReadOnlyMemory<T>.Empty, isFinalWrite: true, CancellationToken.None);
    }
}
