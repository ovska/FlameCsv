using System.Diagnostics.CodeAnalysis;

namespace FlameCsv.ParallelUtils;

internal interface IScheduler<TState> : IDisposable
    where TState : IDisposable
{
    bool TryGetUnconsumedState([NotNullWhen(true)] out TState? state);

    /// <summary>
    /// Schedules work to be done.
    /// </summary>
    /// <param name="state">The state to schedule.</param>
    /// <param name="order">Order of the chunk.</param>
    /// <param name="index">Index of the state within the current chunk, or -1 if this is the last item for this chunk.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask ScheduleAsync(TState state, int order, int index, CancellationToken cancellationToken);

    /// <summary>
    /// Finalizes the scheduler, writing any remaining states to the writer
    /// </summary>
    ValueTask FinalizeAsync(CancellationToken cancellationToken);
}
