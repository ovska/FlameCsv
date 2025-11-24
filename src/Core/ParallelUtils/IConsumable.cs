namespace FlameCsv.ParallelUtils;

internal interface IConsumable
{
    bool ShouldConsume { get; }
}
