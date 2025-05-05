namespace FlameCsv.IO;

/// <summary>
/// Internal implementation detail. Reads raw data from an inner source.
/// </summary>
/// <typeparam name="T"></typeparam>
public interface ICsvBufferReader<T> : IDisposable, IAsyncDisposable where T : unmanaged
{
    /// <summary>
    /// Reads from the inner data source.
    /// </summary>
    /// <returns>
    /// The current data available from the reader,
    /// and whether any more data can be read.
    /// </returns>
    CsvReadResult<T> Read();

    /// <inheritdoc cref="Read"/>
    ValueTask<CsvReadResult<T>> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances the reader by the specified number of characters.
    /// </summary>
    /// <param name="count">Number of characters processed from the read result</param>
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
