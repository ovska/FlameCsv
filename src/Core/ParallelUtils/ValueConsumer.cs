using FlameCsv.Extensions;

namespace FlameCsv.ParallelUtils;

internal sealed class ValueConsumer<TValue>(
    Action<List<TValue>> sink,
    Func<List<TValue>, CancellationToken, ValueTask> asyncSink,
    int chunkSize
) : IConsumer<List<TValue>>
{
    public void Consume(in List<TValue> output) => sink(output);

    public ValueTask ConsumeAsync(List<TValue> output, CancellationToken cancellationToken) =>
        asyncSink(output, cancellationToken);

    public void Finalize(in List<TValue> output, Exception? exception) { }

    public ValueTask FinalizeAsync(List<TValue> output, Exception? exception) => default;

    public void OnException(Exception exception) => exception.Rethrow();

    public bool ShouldConsume(in List<TValue> output) => output.Count >= chunkSize;
}
