using System.Buffers;
using System.Text;
using FlameCsv.Extensions;
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
            if (encoding is null || encoding.Equals(Encoding.UTF8))
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
            WriteHeaderIfNeeded(dematerializer, in writer);

            if (values.TryGetNonEnumeratedCount(out int count) && count == 0)
            {
                EnsureTrailingNewline(in writer);
                return;
            }

            using IEnumerator<TValue> enumerator = values.GetEnumerator();

            if (!enumerator.MoveNext())
            {
                EnsureTrailingNewline(in writer);
                return;
            }

            do
            {
                if (writer.Writer.NeedsFlush)
                {
                    writer.Writer.Flush();
                }

                dematerializer.Write(in writer, enumerator.Current);
                writer.WriteNewline();
            } while (enumerator.MoveNext());
        }
        catch (Exception e)
        {
            // store exception so the writer knows not to flush when completing
            exception = e;
        }
        finally
        {
            using (writer)
            {
                writer.Writer.Complete(exception);
            }
        }

        exception?.Rethrow();
    }

    private static async Task WriteAsyncCore<T, TValue>(
        IEnumerable<TValue> values,
        ValueTask<CsvFieldWriter<T>> writerTask,
        IDematerializer<T, TValue> dematerializer,
        CancellationToken cancellationToken
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        using CsvFieldWriter<T> writer = await writerTask.ConfigureAwait(false);
        Exception? exception = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            WriteHeaderIfNeeded(dematerializer, in writer);

            if (values.TryGetNonEnumeratedCount(out int count) && count == 0)
            {
                EnsureTrailingNewline(in writer);
                return;
            }

            using IEnumerator<TValue> enumerator = values.GetEnumerator();

            if (!enumerator.MoveNext())
            {
                EnsureTrailingNewline(in writer);
                return;
            }

            do
            {
                if (writer.Writer.NeedsFlush)
                {
                    await writer.Writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                dematerializer.Write(in writer, enumerator.Current);
                writer.WriteNewline();
            } while (enumerator.MoveNext());
        }
        catch (Exception e)
        {
            // store exception so the writer knows not to flush when completing
            exception = e;
        }
        finally
        {
            if (cancellationToken.IsCancellationRequested)
            {
                exception ??= new OperationCanceledException(cancellationToken);
            }

            await writer.Writer.CompleteAsync(exception, cancellationToken).ConfigureAwait(false);
        }

        exception?.Rethrow();
    }

    private static async Task WriteAsyncCore<T, TValue>(
        IAsyncEnumerable<TValue> values,
        ValueTask<CsvFieldWriter<T>> writerTask,
        IDematerializer<T, TValue> dematerializer,
        CancellationToken cancellationToken
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        using CsvFieldWriter<T> writer = await writerTask.ConfigureAwait(false);
        Exception? exception = null;

        try
        {
            WriteHeaderIfNeeded(dematerializer, in writer);

            IAsyncEnumerator<TValue> enumerator = values.GetAsyncEnumerator(cancellationToken);

            await using (enumerator)
            {
                if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    EnsureTrailingNewline(in writer);
                    return;
                }

                do
                {
                    if (writer.Writer.NeedsFlush)
                    {
                        await writer.Writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }

                    dematerializer.Write(in writer, enumerator.Current);
                    writer.WriteNewline();
                } while (await enumerator.MoveNextAsync().ConfigureAwait(false));
            }
        }
        catch (Exception e)
        {
            // store exception so the writer knows not to flush when completing
            exception = e;
        }
        finally
        {
            if (cancellationToken.IsCancellationRequested)
            {
                exception ??= new OperationCanceledException(cancellationToken);
            }

            await writer.Writer.CompleteAsync(exception, cancellationToken).ConfigureAwait(false);
        }

        exception?.Rethrow();
    }

    /// <summary>
    /// Writes the header record if <see cref="CsvOptions{T}.HasHeader"/> is <c>true</c>.
    /// </summary>
    private static void WriteHeaderIfNeeded<T, TValue>(
        IDematerializer<T, TValue> dematerializer,
        ref readonly CsvFieldWriter<T> writer
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        if (writer.Options.HasHeader)
        {
            dematerializer.WriteHeader(in writer);
            writer.WriteNewline();
        }
    }

    /// <summary>
    /// If a header record hasn't been written, writes a single newline.
    /// </summary>
    private static void EnsureTrailingNewline<T>(ref readonly CsvFieldWriter<T> writer)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (!writer.Options.HasHeader)
        {
            writer.WriteNewline();
        }
    }
}
