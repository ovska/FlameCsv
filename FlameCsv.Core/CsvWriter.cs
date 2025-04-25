using System.Text;
using FlameCsv.IO;
using FlameCsv.Writing;
using JetBrains.Annotations;

// ReSharper disable InlineTemporaryVariable

namespace FlameCsv;

/// <summary>
/// Provides static methods for writing CSV data.
/// </summary>
[PublicAPI]
public static partial class CsvWriter
{
    /// <summary>
    /// Default buffer size for writing CSV data to a file (32 KiB).
    /// </summary>
    public const int DefaultFileStreamBufferSize = 32 * 1024;

    private static StreamWriter GetFileStreamWriter(
        string path,
        Encoding? encoding,
        bool isAsync,
        in CsvIOOptions ioOptions = default)
    {
        int bufferSize = ioOptions.HasCustomBufferSize
            ? ioOptions.BufferSize
            : DefaultFileStreamBufferSize;

        return new StreamWriter(GetFileStream(path, isAsync, in ioOptions), encoding, bufferSize, leaveOpen: false);
    }

    private static FileStream GetFileStream(string path, bool isAsync, in CsvIOOptions ioOptions = default)
    {
        int bufferSize = ioOptions.HasCustomBufferSize
            ? ioOptions.BufferSize
            : DefaultFileStreamBufferSize;

        return new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize,
            FileOptions.SequentialScan | (isAsync ? FileOptions.Asynchronous : FileOptions.None));
    }

    private static void WriteCore<T, TValue>(
        IEnumerable<TValue> values,
        CsvFieldWriter<T> writer,
        IDematerializer<T, TValue> dematerializer)
        where T : unmanaged, IBinaryInteger<T>
    {
        Exception? exception = null;

        try
        {
            if (writer.Options.HasHeader)
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

            if (writer.Options.HasHeader)
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

            if (writer.Options.HasHeader)
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
            // re-throws exceptions
            await writer.Writer.CompleteAsync(exception, cancellationToken).ConfigureAwait(false);
        }
    }
}
