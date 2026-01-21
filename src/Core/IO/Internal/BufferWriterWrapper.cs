using System.Buffers;

namespace FlameCsv.IO.Internal;

internal sealed class BufferWriterWrapper<T>(IBufferWriter<T> writer, IBufferPool? pool) : ICsvBufferWriter<T>
    where T : unmanaged
{
    public IBufferPool BufferPool => pool ?? DefaultBufferPool.Instance;

    public bool NeedsFlush => false;

    public void Advance(int count) => writer.Advance(count);

    public void Complete(Exception? exception) { }

    public ValueTask CompleteAsync(Exception? exception, CancellationToken cancellationToken = default)
    {
        return cancellationToken.IsCancellationRequested
            ? ValueTask.FromCanceled(cancellationToken)
            : ValueTask.CompletedTask;
    }

    public void Flush() { }

    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        return cancellationToken.IsCancellationRequested
            ? ValueTask.FromCanceled(cancellationToken)
            : ValueTask.CompletedTask;
    }

    public Memory<T> GetMemory(int sizeHint = 0)
    {
        return writer.GetMemory(sizeHint);
    }

    public Span<T> GetSpan(int sizeHint = 0)
    {
        return writer.GetSpan(sizeHint);
    }
}
