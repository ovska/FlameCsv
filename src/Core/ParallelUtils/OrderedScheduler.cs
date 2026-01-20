using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace FlameCsv.ParallelUtils;

internal sealed class OrderedScheduler<TState>(ChannelWriter<TState> writer) : IScheduler<TState>
    where TState : IDisposable
{
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _orderSemaphores = new()
    {
        [0] = new SemaphoreSlim(initialCount: 1, maxCount: 1),
    };

    public void Dispose()
    {
        foreach (SemaphoreSlim semaphore in _orderSemaphores.Values)
        {
            semaphore.Dispose();
        }
    }

    public ValueTask FinalizeAsync(CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }

    public bool TryGetUnconsumedState([NotNullWhen(true)] out TState? state)
    {
        // ordered scheduler always needs a fresh state for a worker
        state = default;
        return false;
    }

    public async ValueTask ScheduleAsync(TState state, int order, int index, CancellationToken cancellationToken)
    {
        SemaphoreSlim current = GetOrCreateSemaphore(order);

        await current.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await writer.WriteAsync(state, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (index < 0)
            {
                SemaphoreSlim next = GetOrCreateSemaphore(order + 1);
                next.Release();
            }
            else
            {
                current.Release();
            }
        }
    }

    private SemaphoreSlim GetOrCreateSemaphore(int order)
    {
        return _orderSemaphores.GetOrAdd(
            order,
            static order => new SemaphoreSlim(initialCount: order == 0 ? 1 : 0, maxCount: 1)
        );
    }
}
