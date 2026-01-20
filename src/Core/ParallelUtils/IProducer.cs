namespace FlameCsv.ParallelUtils;

/// <summary>
/// Processes states for use in a parallel processing loop and yields them to a consumer.
/// </summary>
/// <typeparam name="TInput">Data type for the input</typeparam>
/// <typeparam name="TState">State that accumulates the inputs</typeparam>
/// <typeparam name="TChunk">Type of chunk yielded to the consumer</typeparam>
internal interface IProducer<TInput, TState, TChunk>
    where TInput : allows ref struct
    where TChunk : IHasOrder
{
    /// <summary>
    /// A single task to run before the main loop is started.
    /// </summary>
    void BeforeLoop(CancellationToken cancellationToken);

    /// <summary>
    /// An asynchronous single task to run before the main loop is started.
    /// </summary>
    ValueTask BeforeLoopAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Processes the shared with a given input.
    /// </summary>
    void Produce(TChunk chunk, TInput input, TState state);

    /// <summary>
    /// Creates a new state.
    /// </summary>
    TState CreateState();
}
