using System.Buffers;
using System.ComponentModel;
using JetBrains.Annotations;

namespace FlameCsv.IO;

/// <summary>
/// Internal implementation detail.
/// </summary>
[PublicAPI]
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

    /// <summary>
    /// Completes the reader, flushing unflushed data if <paramref name="exception"/> is <see langword="null"/>.
    /// </summary>
    /// <param name="exception">
    /// Exception observed when writing the data. If null, pending unflushed data does not get flushed.
    /// </param>
    void Complete(Exception? exception);

    /// <inheritdoc cref="Flush" />
    ValueTask FlushAsync(CancellationToken cancellationToken = default);

    /// <inheritdoc cref="Complete" />
    ValueTask CompleteAsync(
        Exception? exception,
        CancellationToken cancellationToken = default);
}
