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
    /// Whether the writer should be flushed to prevent resizing internal buffers.
    /// </summary>
    bool NeedsFlush { get; }

    /// <summary>
    /// Flushes the writer, ensuring that the written data is transported to the underlying target.
    /// </summary>
    void Flush();

    /// <summary>
    /// Completes the reader, flushing unflushed data if <paramref name="exception"/> is <see langword="null"/>.
    /// </summary>
    /// <param name="exception">
    /// Exception observed when writing the data. If not null, pending unflushed data does not get flushed.
    /// </param>
    void Complete(Exception? exception);

    /// <summary>
    /// Asynchronously flushes the writer, ensuring that the written data is transported to the underlying target.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the flush operation</param>
    /// <returns>A task representing the flush operation</returns>
    ValueTask FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously completes the writer, flushing unflushed data if <paramref name="exception"/> is <see langword="null"/>.
    /// </summary>
    /// <param name="exception">
    /// Exception observed when writing the data. If not null, pending unflushed data does not get flushed.
    /// </param>
    /// <param name="cancellationToken">Cancellation token to cancel the flushing operation if applicable</param>
    /// <returns>A task representing the completion operation</returns>
    ValueTask CompleteAsync(Exception? exception, CancellationToken cancellationToken = default);
}
