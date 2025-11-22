namespace FlameCsv.ParallelUtils;

internal interface IHasOrder
{
    int Order{ get; }
}

/// <summary>
/// Consumes states produced by a <see cref="IProducer{TInput, TState}"/> in a parallel processing loop.
/// </summary>
/// <typeparam name="TState">State that a producer has processed</typeparam>
internal interface IConsumer<TState>
{
    /// <summary>
    /// Whether the given output state should be consumed.
    /// </summary>
    /// <param name="output">Output to consume</param>
    bool ShouldConsume(in TState output);

    /// <summary>
    /// Consumes the given output state.
    /// </summary>
    void Consume(in TState output);

    /// <summary>
    /// Asynchronously consumes the given output state.
    /// </summary>
    ValueTask ConsumeAsync(TState output, CancellationToken cancellationToken);

    /// <summary>
    /// Finalizes the given output state. This must be called after the state has been consumed.
    /// </summary>
    /// <param name="output">Output to finalize</param>
    /// <param name="exception">Exception that occurred during <see cref="Consume"/></param>
    void Finalize(in TState output, Exception? exception);

    /// <summary>
    /// Asynchronously finalizes the given output state. This must be called after the state has
    /// been consumed.
    /// </summary>
    /// <param name="output">Output to finalize</param>
    /// <param name="exception">Exception that occurred during <see cref="ConsumeAsync"/></param>
    ValueTask FinalizeAsync(TState output, Exception? exception);

    /// <summary>
    /// Notifies the consumer that an exception has occurred during consuming.
    /// </summary>
    void OnException(Exception exception);
}
