using System.Buffers;

namespace FlameCsv.Writing;

public interface ICsvBufferWriter<T> : IBufferWriter<T> where T : unmanaged
{
    /// <summary>
    /// Whether the writer's buffer is nearly full and should be flushed.
    /// </summary>
    bool NeedsFlush { get; }

    /// <inheritdoc cref="FlushAsync(CancellationToken)" />
    void Flush();

    /// <inheritdoc cref="CompleteAsync(Exception?, CancellationToken)" />
    void Complete(Exception? exception);

    /// <summary>
    /// Flushes the writer.
    /// </summary>
    ValueTask FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes the reader, flushing unflushed data if no exceptions.
    /// </summary>
    /// <param name="exception">
    /// Exception observed when writing the data. If null, pending unflushed data does not get flushed.
    /// </param>
    /// <param name="cancellationToken"></param>
    ValueTask CompleteAsync(
        Exception? exception,
        CancellationToken cancellationToken = default);
}
