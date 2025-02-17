using System.IO.Pipelines;

namespace FlameCsv.Reading.Internal;

// wrap pipereader to use as ICsvPipeReader<> to avoid duplicate code
internal sealed class PipeReaderWrapper(PipeReader inner) : ICsvPipeReader<byte>
{
    public bool SupportsSynchronousReads => false;

    public void AdvanceTo(SequencePosition consumed, SequencePosition examined) => inner.AdvanceTo(consumed, examined);
    public bool TryReset() => false;

    public ValueTask DisposeAsync() => inner.CompleteAsync(exception: null);
    public void Dispose() => inner.Complete(exception: null);

    public CsvReadResult<byte> Read()
        => throw new NotSupportedException("PipeReader does not support synchronous reads");

    public ValueTask<CsvReadResult<byte>> ReadAsync(CancellationToken cancellationToken = default)
    {
        var result = inner.ReadAsync(cancellationToken);

        if (result.IsCompletedSuccessfully)
        {
            var value = result.GetAwaiter().GetResult();
            return new(new CsvReadResult<byte>(value.Buffer, value.IsCompleted));
        }

        return Core(result);

        static async ValueTask<CsvReadResult<byte>> Core(ValueTask<ReadResult> readTask)
        {
            var result = await readTask.ConfigureAwait(false);
            return new CsvReadResult<byte>(result.Buffer, result.IsCompleted);
        }
    }
}
