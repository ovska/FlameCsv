namespace FlameCsv.ParallelUtils;

/// <summary>
/// Indicates that type can be consumed in a parallel loop.
/// </summary>
internal interface IConsumable : IDisposable
{
    /// <summary>
    /// Whether the type is ready to be consumed.
    /// </summary>
    bool ShouldConsume { get; }
}

internal interface IConsumer<TState>
{
    void Consume(in TState state, Exception? ex);
    ValueTask ConsumeAsync(TState state, Exception? ex, CancellationToken cancellationToken);
}
