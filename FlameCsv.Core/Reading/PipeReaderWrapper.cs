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

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public async ValueTask<CsvReadResult<byte>> ReadAsync(CancellationToken cancellationToken = default)
    {
        var result = await inner.ReadAsync(cancellationToken).ConfigureAwait(false);
        return new CsvReadResult<byte>(result.Buffer, result.IsCompleted);
    }
}
