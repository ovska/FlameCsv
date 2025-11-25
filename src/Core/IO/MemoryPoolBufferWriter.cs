using System.Buffers;
using FlameCsv.Extensions;
using FlameCsv.IO.Internal;

namespace FlameCsv.IO;

internal sealed class MemoryPoolBufferWriter<T> : CsvBufferWriter<T>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly Action<ReadOnlySpan<T>>? _onFlush;
    private readonly Func<ReadOnlyMemory<T>, CancellationToken, ValueTask>? _onFlushAsync;

    public MemoryPoolBufferWriter(
        Action<ReadOnlySpan<T>>? onFlush,
        Func<ReadOnlyMemory<T>, CancellationToken, ValueTask>? onFlushAsync,
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

    protected override ValueTask FlushAsyncCore(ReadOnlyMemory<T> memory, CancellationToken cancellationToken)
    {
        if (_onFlushAsync is null)
        {
            Throw.NotSupported("Async flush delegate was not provided in constructor");
        }

        return _onFlushAsync(memory, cancellationToken);
    }

    protected override void FlushCore(ReadOnlyMemory<T> memory)
    {
        if (_onFlush is null)
        {
            Throw.NotSupported("Flush delegate was not provided in constructor");
        }

        _onFlush(memory.Span);
    }
}
