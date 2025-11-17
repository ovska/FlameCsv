using System.Runtime.CompilerServices;
using FlameCsv.Exceptions;
using FlameCsv.Writing;

namespace FlameCsv.ParallelUtils;

/// <summary>
/// Consumer of field writers.
/// </summary>
internal readonly struct CsvWriterConsumer<T> : IConsumer<CsvFieldWriter<T>>
    where T : unmanaged, IBinaryInteger<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ShouldConsume(in CsvFieldWriter<T> output)
    {
        return output.Writer.NeedsFlush;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Consume(in CsvFieldWriter<T> output)
    {
        output.Writer.Flush();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Finalize(in CsvFieldWriter<T> output, Exception? exception)
    {
        output.Writer.Complete(exception);
        output.Dispose();
    }

    public ValueTask ConsumeAsync(CsvFieldWriter<T> output, CancellationToken cancellationToken)
    {
        return output.Writer.FlushAsync(cancellationToken);
    }

    public ValueTask FinalizeAsync(CsvFieldWriter<T> output, Exception? exception)
    {
        return output.Writer.CompleteAsync(exception);
    }

    public void OnException(Exception exception)
    {
        throw new CsvWriteException(
            "An error occurred while flushing written CSV data to the underlying target.",
            exception
        );
    }
}
