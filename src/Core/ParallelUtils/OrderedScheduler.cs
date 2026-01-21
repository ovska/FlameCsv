using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace FlameCsv.ParallelUtils;

internal sealed class OrderedScheduler<TState> : IScheduler<TState>
    where TState : IDisposable
{
    private readonly ConcurrentDictionary<int, ScheduleOrder> _orderTasks = [];
    private readonly ChannelWriter<TState> _writer;
    private readonly CancellationToken _cancellationToken;

    public OrderedScheduler(ChannelWriter<TState> writer, CancellationToken cancellationToken)
    {
        _writer = writer;
        _cancellationToken = cancellationToken;
    }

    public void Dispose()
    {
        // clean up any remaining registrations on exceptions
        foreach (var kvp in _orderTasks)
        {
            kvp.Value.Registration.Dispose();
        }
    }

    public ValueTask FinalizeAsync()
    {
#if DEBUG || FUZZ || FULL_TEST_SUITE
        var ode = new ObjectDisposedException(GetType().FullName);
        foreach (var kvp in _orderTasks)
        {
            kvp.Value.Source.TrySetException(ode);
        }
#endif
        // no remaining states to flush in ordered scheduler
        return ValueTask.CompletedTask;
    }

    public bool TryGetUnconsumedState([NotNullWhen(true)] out TState? state)
    {
        // ordered scheduler always needs a fresh state for a worker
        state = default;
        return false;
    }

    public ValueTask ScheduleAsync(TState state, int order, bool isLast = false)
    {
        ScheduleOrder current = GetOrCreate(order);

        if (current.Source.Task.IsCompletedSuccessfully && !isLast)
        {
            return _writer.WriteAsync(state, _cancellationToken);
        }

        return ScheduleAsyncAwaited(current, state, order, isLast);
    }

    private async ValueTask ScheduleAsyncAwaited(ScheduleOrder current, TState state, int order, bool isLast)
    {
        await current.Source.Task.ConfigureAwait(false);
        await _writer.WriteAsync(state, _cancellationToken).ConfigureAwait(false);

        if (isLast)
        {
            // allow next item to proceed
            GetOrCreate(order + 1).Source.SetResult();

            // clean up current
            current.Registration.Dispose();
            _orderTasks.TryRemove(order, out _);
        }
    }

    private ScheduleOrder GetOrCreate(int order)
    {
        return _orderTasks.GetOrAdd(
            order,
            static (order, @this) =>
            {
                TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
                CancellationTokenRegistration registration;

                if (order == 0)
                {
                    registration = default;
                    tcs.SetResult();
                }
                else
                {
                    registration = @this._cancellationToken.UnsafeRegister(
                        static state => ((TaskCompletionSource)state!).TrySetCanceled(),
                        tcs
                    );
                }

                return new ScheduleOrder(tcs, registration);
            },
            this
        );
    }
}

internal readonly record struct ScheduleOrder(TaskCompletionSource Source, CancellationTokenRegistration Registration);
