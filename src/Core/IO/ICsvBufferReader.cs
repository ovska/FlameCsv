namespace FlameCsv.IO;

/// <summary>
/// Buffer reader interface for reading CSV data.
/// </summary>
public interface ICsvBufferReader<T> : IDisposable, IAsyncDisposable
    where T : unmanaged
{
    /// <summary>
    /// Number of <typeparamref name="T"/> that have been read from the underlying data source.
    /// </summary>
    /// <remarks>
    /// The position is updated after each <em>read</em> operation, not advance.
    /// </remarks>
    long Position { get; }

    /// <summary>
    /// Reads from the inner data source.
    /// </summary>
    /// <returns>
    /// The current data available from the reader, and whether any more data can be read.
    /// </returns>
    CsvReadResult<T> Read();

    /// <summary>
    /// Asynchronously reads data from the inner data source.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the read operation</param>
    /// <returns>
    /// A task returning the current data available from the reader, and whether any more data can be read.
    /// </returns>
    ValueTask<CsvReadResult<T>> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances the reader by the specified number of characters.
    /// </summary>
    /// <param name="count">Number of characters processed from the read result</param>
    /// <remarks>
    /// Similar to a pipe reader, advancing the reader invalidates the data in the previous read result.
    /// </remarks>
    void Advance(int count);

    /// <summary>
    /// Attempts to reset the reader to the beginning of the data source.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the reader was reset successfully; <c>false</c> if the reader does not support resetting.
    /// </returns>
    /// <exception cref="ObjectDisposedException"/>
    bool TryReset();
}
