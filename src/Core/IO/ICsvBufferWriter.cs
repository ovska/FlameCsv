using System.Buffers;
using System.ComponentModel;
using JetBrains.Annotations;

namespace FlameCsv.IO;

/// <summary>
/// Buffer writer interface for writing CSV data.
/// </summary>
/// <remarks>
/// This interface extends <see cref="IBufferWriter{T}"/> to provide additional functionality to flush and complete the writer.
/// </remarks>
[PublicAPI]
[EditorBrowsable(EditorBrowsableState.Advanced)]
public interface ICsvBufferWriter<T> : IBufferWriter<T>
    where T : unmanaged
{
    /// <summary>
    /// Gets the buffer pool used by the writer.
    /// </summary>
    public IBufferPool BufferPool { get; }

    /// <summary>
    /// Whether the writer should be drained to prevent resizing internal buffers.
    /// </summary>
    bool NeedsDrain { get; }

    /// <summary>
    /// Drains the writer, ensuring that the buffered data is written to the underlying target.
    /// <br/>May be a no-op if the writer does not buffer data.
    /// </summary>
    void Drain();

    /// <summary>
    /// Completes the writer, draining unflushed data and flushing the underlying target if <paramref name="exception"/> is <see langword="null"/>.
    /// </summary>
    /// <param name="exception">Exception observed when writing the data. If not null, pending data may not be flushed.</param>
    void Complete(Exception? exception);

    /// <summary>
    /// Asynchronously drains the writer, ensuring that the buffered data is written to the underlying target.
    /// <br/>May be a no-op if the writer does not buffer data.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the flush operation</param>
    /// <returns>A task representing the flush operation</returns>
    ValueTask DrainAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously completes the writer, draining unflushed data and flushing the underlying target if <paramref name="exception"/> is <see langword="null"/>.
    /// </summary>
    /// <param name="exception">Exception observed when writing the data. If not null, pending data may not be flushed.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the flushing operation if applicable</param>
    /// <returns>A task representing the completion operation</returns>
    ValueTask CompleteAsync(Exception? exception, CancellationToken cancellationToken = default);
}
