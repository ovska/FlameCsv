using System.Buffers;

namespace FlameCsv.Writers;

internal interface IAsyncBufferWriter<T> : IBufferWriter<T> where T : unmanaged
{
    /// <summary>
    /// Whether the writer's buffer is nearly full and it needs to be flushed.
    /// </summary>
    bool NeedsFlush { get; }

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
