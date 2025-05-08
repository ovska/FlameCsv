using System.Buffers;
using System.Text;
using FlameCsv.IO;
using FlameCsv.IO.Internal;
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
    private static FileStream GetFileStream(string path, bool isAsync, in CsvIOOptions ioOptions)
    {
        return new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            ioOptions.BufferSize,
            FileOptions.SequentialScan | (isAsync ? FileOptions.Asynchronous : FileOptions.None)
        );
    }

    private static ICsvBufferWriter<char> GetFileBufferWriter(
        string path,
        Encoding? encoding,
        bool isAsync,
        MemoryPool<char> memoryPool,
        CsvIOOptions ioOptions
    )
    {
        FileStream stream = GetFileStream(path, isAsync, in ioOptions);

        try
        {
            if (encoding is null || encoding.Equals(Encoding.UTF8) || encoding.Equals(Encoding.ASCII))
            {
                return new Utf8StreamWriter(stream, memoryPool, in ioOptions);
            }

            return new TextBufferWriter(
                new StreamWriter(stream, encoding, ioOptions.BufferSize, leaveOpen: false),
                memoryPool ?? MemoryPool<char>.Shared,
                ioOptions
            );
        }
        catch
        {
            // exception before we returned control to the caller
            stream.Dispose();
            throw;
        }
    }

    private static void WriteCore<T, TValue>(
        IEnumerable<TValue> values,
        CsvFieldWriter<T> writer,
        IDematerializer<T, TValue> dematerializer
    )
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
                {
                    writer.Writer.Flush();
                }

                dematerializer.Write(in writer, value);
                writer.WriteNewline();
            }
        }
        catch (Exception e)
        {
            // store exception so the writer knows not to flush when completing
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
        ValueTask<CsvFieldWriter<T>> writerTask,
        IDematerializer<T, TValue> dematerializer,
        CancellationToken cancellationToken
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        CsvFieldWriter<T> writer = await writerTask.ConfigureAwait(false);
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
                {
                    await writer.Writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                dematerializer.Write(in writer, value);
                writer.WriteNewline();
            }
        }
        catch (Exception e)
        {
            // store exception so the writer knows not to flush when completing
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
        ValueTask<CsvFieldWriter<T>> writerTask,
        IDematerializer<T, TValue> dematerializer,
        CancellationToken cancellationToken
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        CsvFieldWriter<T> writer = await writerTask.ConfigureAwait(false);
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
                {
                    await writer.Writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                dematerializer.Write(in writer, value);
                writer.WriteNewline();
            }
        }
        catch (Exception e)
        {
            // store exception so the writer knows not to flush when completing
            exception = e;
        }
        finally
        {
            // re-throws exceptions
            await writer.Writer.CompleteAsync(exception, cancellationToken).ConfigureAwait(false);
        }
    }
}
