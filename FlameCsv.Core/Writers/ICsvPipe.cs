namespace FlameCsv.Writers;

internal interface ICsvPipe<T> where T : unmanaged
{
    /// <summary>
    /// Returns a buffer of unspecified size that can be written to.
    /// </summary>
    /// <seealso cref="GrowAsync"/>
    Span<T> GetSpan();

    /// <summary>
    /// Returns a buffer of unspecified size that can be written to.
    /// </summary>
    /// <seealso cref="GrowAsync"/>
    Memory<T> GetMemory();

    /// <summary>
    /// Signals that the specified amount of tokens have been written to the buffer.
    /// </summary>
    /// <param name="length">Tokens written</param>
    void Advance(int length);

    /// <summary>
    /// Grows the buffer to be guaranteed to be larger than <paramref name="previousBufferSize"/>,
    /// either by flushing the data or increasing the buffer size.
    /// </summary>
    /// <param name="previousBufferSize">Size of the previous buffer that was too small</param>
    /// <param name="cancellationToken">Token to cancel a possible flush</param>
    /// <returns>A ValueTask representing the completion of the flush operation.</returns>
    ValueTask GrowAsync(int previousBufferSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Manually flushes pending data. Called automatically by <see cref="GrowAsync"/> and
    /// <see cref="IAsyncDisposable.DisposeAsync"/>.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation</param>
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
