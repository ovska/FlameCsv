namespace FlameCsv.Writers;

internal interface ICsvWriter<T> : IAsyncDisposable where T : unmanaged
{
    /// <summary>
    /// Amount of unflushed data in the writer.
    /// </summary>
    int Unflushed { get; }

    /// <summary>
    /// Exception observed when writing the data.
    /// When set to non-null, pending unflushed data does not get flushed.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Returns a buffer of unspecified size that can be written to.
    /// </summary>
    /// <seealso cref="GrowAsync"/>
    Span<T> GetBuffer();

    /// <summary>
    /// Signals that the specified amount of tokens have been written to the buffer.
    /// </summary>
    /// <param name="length">Tokens written</param>
    void Advance(int length);

    /// <summary>
    /// Grows the buffer, either by flushing the data or increasing the buffer size.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel a possible flush</param>
    /// <returns>
    /// A ValueTask containing a buffer that is guaranteed to be larger than the previous buffer received from
    /// <see cref="GetBuffer"/> or <see cref="GrowAsync"/>.
    /// </returns>
    ValueTask<Memory<T>> GrowAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Manually flushes pending data. Called automatically by <see cref="GrowAsync"/> and
    /// <see cref="IAsyncDisposable.DisposeAsync"/>.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A ValueTask representing the completion of the flush operation.</returns>
    ValueTask FlushAsync(CancellationToken cancellationToken = default);
}
