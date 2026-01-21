using System.Diagnostics.CodeAnalysis;

namespace FlameCsv.ParallelUtils;

internal interface IScheduler<TState> : IDisposable
    where TState : IDisposable
{
    /// <summary>
    /// Attempts to get a yet unconsumed state.
    /// </summary>
    bool TryGetUnconsumedState([NotNullWhen(true)] out TState? state);

    /// <summary>
    /// Schedules work to be done.
    /// </summary>
    /// <param name="state">The state to schedule.</param>
    /// <param name="order">Order of the chunk.</param>
    /// <param name="isLast">Indicates if this is the last item for this chunk.</param>
    ValueTask ScheduleAsync(TState state, int order, bool isLast = false);

    /// <summary>
    /// Finalizes the scheduler, writing any remaining states to the writer
    /// </summary>
    ValueTask FinalizeAsync();
}
