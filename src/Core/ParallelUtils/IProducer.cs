namespace FlameCsv.ParallelUtils;

/// <summary>
/// Processes states for use in a parallel processing loop and yields them to a <see cref="IConsumer{TState}"/>.
/// </summary>
/// <typeparam name="TInput">Data type for the input</typeparam>
/// <typeparam name="TState">State that accumulates the inputs</typeparam>
internal interface IProducer<TInput, TState> : IDisposable
    where TInput : allows ref struct
{
    /// <summary>
    /// A single task to run before the main loop is started.
    /// </summary>
    void BeforeLoop();

    /// <summary>
    /// An asynchronous single task to run before the main loop is started.
    /// </summary>
    ValueTask BeforeLoopAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Processes the shared with a given input.
    /// </summary>
    void Produce(int order, TInput input, ref TState state);

    /// <summary>
    /// Creates a new state.
    /// </summary>
    TState CreateState();

    /// <summary>
    /// Notifies the producer that an exception has occurred during producing.
    /// </summary>
    void OnException(Exception exception);
}
