using FlameCsv.Writing;
using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// Provides static methods for writing CSV data.
/// </summary>
[PublicAPI]
static partial class CsvWriter
{
    /// <summary>
    /// Default buffer size for writing CSV data.
    /// </summary>
    public const int DefaultBufferSize = 4096;

    private static void WriteCore<T, TValue>(
        IEnumerable<TValue> values,
        CsvFieldWriter<T> writer,
        IDematerializer<T, TValue> dematerializer)
        where T : unmanaged, IBinaryInteger<T>
    {
        Exception? exception = null;

        try
        {
            if (writer.Options._hasHeader)
            {
                dematerializer.WriteHeader(in writer);
                writer.WriteNewline();
            }

            foreach (var value in values)
            {
                if (writer.Writer.NeedsFlush)
                    writer.Writer.Flush();

                dematerializer.Write(in writer, value);
                writer.WriteNewline();
            }
        }
        catch (Exception e)
        {
            // store exception so the writer knows not to flush when disposing
            exception = e;
        }
        finally
        {
            // re-throws exceptions
            writer.Writer.Complete(exception);
        }
    }

    private static async Task WriteAsyncCore<T, TValue>(
        IEnumerable<TValue> values,
        CsvFieldWriter<T> writer,
        IDematerializer<T, TValue> dematerializer,
        CancellationToken cancellationToken)
        where T : unmanaged, IBinaryInteger<T>
    {
        Exception? exception = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (writer.Options._hasHeader)
            {
                dematerializer.WriteHeader(in writer);
                writer.WriteNewline();
            }

            foreach (var value in values)
            {
                if (writer.Writer.NeedsFlush)
                    await writer.Writer.FlushAsync(cancellationToken).ConfigureAwait(false);

                dematerializer.Write(in writer, value);
                writer.WriteNewline();
            }
        }
        catch (Exception e)
        {
            // store exception so the writer knows not to flush when disposing
            exception = e;
        }
        finally
        {
            if (cancellationToken.IsCancellationRequested)
                exception ??= new OperationCanceledException(cancellationToken);

            // re-throws exceptions
            await writer.Writer.CompleteAsync(exception, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task WriteAsyncCore<T, TValue>(
        IAsyncEnumerable<TValue> values,
        CsvFieldWriter<T> writer,
        IDematerializer<T, TValue> dematerializer,
        CancellationToken cancellationToken)
        where T : unmanaged, IBinaryInteger<T>
    {
        Exception? exception = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (writer.Options._hasHeader)
            {
                dematerializer.WriteHeader(in writer);
                writer.WriteNewline();
            }

            await foreach (var value in values.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (writer.Writer.NeedsFlush)
                    await writer.Writer.FlushAsync(cancellationToken).ConfigureAwait(false);

                dematerializer.Write(in writer, value);
                writer.WriteNewline();
            }
        }
        catch (Exception e)
        {
            // store exception so the writer knows not to flush when disposing
            exception = e;
        }
        finally
        {
            if (cancellationToken.IsCancellationRequested)
                exception ??= new OperationCanceledException(cancellationToken);

            // re-throws exceptions
            await writer.Writer.CompleteAsync(exception, cancellationToken).ConfigureAwait(false);
        }
    }
}
