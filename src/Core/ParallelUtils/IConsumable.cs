namespace FlameCsv.ParallelUtils;

/// <summary>
/// Indicates that type type can be consumed in a parallel loop.
/// </summary>
internal interface IConsumable
{
    /// <summary>
    /// Whether the type is ready to be consumed.
    /// </summary>
    bool ShouldConsume { get; }
}
