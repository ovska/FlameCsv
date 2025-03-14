using System.Buffers;
using System.ComponentModel;

namespace FlameCsv.Writing;

/// <summary>
/// An extended <see cref="IBufferWriter{T}"/> that can be flushed, and can report whether it should be flushed.
/// </summary>
/// <typeparam name="T"></typeparam>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public interface ICsvBufferWriter<T> : IBufferWriter<T> where T : unmanaged
{
    /// <summary>
    /// Whether the possible internal buffer is nearly full and should be flushed.
    /// </summary>
    bool NeedsFlush { get; }

    /// <summary>
    /// Flushes the writer, ensuring that the written data is transported to the underlying target.
    /// </summary>
    void Flush();

    /// <inheritdoc cref="CompleteAsync(Exception?, CancellationToken)" />
    void Complete(Exception? exception);

    /// <inheritdoc cref="Flush" />
    ValueTask FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes the reader, flushing unflushed data if <paramref name="exception"/> is null.
    /// </summary>
    /// <param name="exception">
    /// Exception observed when writing the data. If null, pending unflushed data does not get flushed.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the final flush.</param>
    ValueTask CompleteAsync(
        Exception? exception,
        CancellationToken cancellationToken = default);
}
