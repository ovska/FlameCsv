using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace FlameCsv.ParallelUtils;

internal sealed class UnorderedScheduler<TState>(ChannelWriter<TState> writer) : IScheduler<TState>
    where TState : IDisposable
{
    private readonly ConcurrentStack<TState> _unconsumedStates = [];

    public void Dispose()
    {
        while (_unconsumedStates.TryPop(out TState? remaining))
        {
            remaining.Dispose();
        }
    }

    public async ValueTask FinalizeAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _unconsumedStates.TryPop(out TState? state))
        {
            await writer.WriteAsync(state, cancellationToken).ConfigureAwait(false);
        }
    }

    public ValueTask ScheduleAsync(TState state, int order, int index, CancellationToken cancellationToken)
    {
        // -1 = last chunk, this state is not ready to be consumed yet
        if (index < 0)
        {
            _unconsumedStates.Push(state);
            return ValueTask.CompletedTask;
        }

        return writer.WriteAsync(state, cancellationToken);
    }

    public bool TryGetUnconsumedState([NotNullWhen(true)] out TState? state) => _unconsumedStates.TryPop(out state);
}
