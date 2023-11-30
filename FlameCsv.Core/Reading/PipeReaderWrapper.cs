using System.IO.Pipelines;
using System.Runtime.CompilerServices;

namespace FlameCsv.Reading;

// wrap pipereader to use as ICsvPipeReader<> to avoid duplicate code
internal sealed class PipeReaderWrapper(PipeReader inner) : ICsvPipeReader<byte>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AdvanceTo(SequencePosition consumed, SequencePosition examined) => inner.AdvanceTo(consumed, examined);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask DisposeAsync() => inner.CompleteAsync(exception: null);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<CsvReadResult<byte>> ReadAsync(CancellationToken cancellationToken = default)
    {
        var readTask = inner.ReadAsync(cancellationToken);

        if (readTask.IsCompletedSuccessfully)
        {
            var result = readTask.Result;
            return new(new CsvReadResult<byte>(result.Buffer, result.IsCompleted));
        }

        return Core(readTask);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private static async ValueTask<CsvReadResult<byte>> Core(ValueTask<ReadResult> readTask)
    {
        var result = await readTask.ConfigureAwait(false);
        return new CsvReadResult<byte>(result.Buffer, result.IsCompleted);
    }
}
