using FlameCsv.Extensions;
using FlameCsv.IO;
using FlameCsv.Writing;
using JetBrains.Annotations;

namespace FlameCsv;

[PublicAPI]
internal static class CsvWriter
{
    internal static void WriteCore<T, TValue>(
        IEnumerable<TValue> values,
        Csv.IWriteBuilder<T> builder,
        CsvOptions<T> options,
        IDematerializer<T, TValue> dematerializer
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        Exception? exception = null;

        ICsvBufferWriter<T> bufferWriter = builder.CreateWriter(isAsync: false);

        try
        {
            using CsvFieldWriter<T> writer = new(bufferWriter, options);

            WriteHeaderIfNeeded(dematerializer, writer);

            if (values.TryGetNonEnumeratedCount(out int count) && count == 0)
            {
                EnsureTrailingNewline(writer);
                return;
            }

            using IEnumerator<TValue> enumerator = values.GetEnumerator();

            if (!enumerator.MoveNext())
            {
                EnsureTrailingNewline(writer);
                return;
            }

            do
            {
                if (writer.Writer.NeedsDrain)
                {
                    writer.Writer.Drain();
                }

                dematerializer.Write(writer, enumerator.Current);
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
            bufferWriter.Complete(exception);
        }

        exception?.Rethrow();
    }

    internal static async Task WriteAsyncCore<T, TValue>(
        IEnumerable<TValue> values,
        Csv.IWriteBuilder<T> builder,
        CsvOptions<T> options,
        IDematerializer<T, TValue> dematerializer,
        CancellationToken cancellationToken
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        Exception? exception = null;

        ICsvBufferWriter<T> bufferWriter = builder.CreateWriter(isAsync: true);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            using CsvFieldWriter<T> writer = new(bufferWriter, options);

            WriteHeaderIfNeeded(dematerializer, writer);

            if (values.TryGetNonEnumeratedCount(out int count) && count == 0)
            {
                EnsureTrailingNewline(writer);
                return;
            }

            using IEnumerator<TValue> enumerator = values.GetEnumerator();

            if (!enumerator.MoveNext())
            {
                EnsureTrailingNewline(writer);
                return;
            }

            do
            {
                if (writer.Writer.NeedsDrain)
                {
                    await writer.Writer.DrainAsync(cancellationToken).ConfigureAwait(false);
                }

                dematerializer.Write(writer, enumerator.Current);
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
            exception ??= cancellationToken.GetExceptionIfCanceled();
            await bufferWriter.CompleteAsync(exception, cancellationToken).ConfigureAwait(false);
        }

        exception?.Rethrow();
    }

    internal static async Task WriteAsyncCore<T, TValue>(
        IAsyncEnumerable<TValue> values,
        Csv.IWriteBuilder<T> builder,
        CsvOptions<T> options,
        IDematerializer<T, TValue> dematerializer,
        CancellationToken cancellationToken
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        Exception? exception = null;
        ICsvBufferWriter<T> bufferWriter = builder.CreateWriter(isAsync: true);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            using CsvFieldWriter<T> writer = new(bufferWriter, options);

            WriteHeaderIfNeeded(dematerializer, writer);

            IAsyncEnumerator<TValue> enumerator = values.GetAsyncEnumerator(cancellationToken);

            await using (enumerator.ConfigureAwait(false))
            {
                if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    EnsureTrailingNewline(writer);
                    return;
                }

                do
                {
                    if (writer.Writer.NeedsDrain)
                    {
                        await writer.Writer.DrainAsync(cancellationToken).ConfigureAwait(false);
                    }

                    dematerializer.Write(writer, enumerator.Current);
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
            exception ??= cancellationToken.GetExceptionIfCanceled();
            await bufferWriter.CompleteAsync(exception, cancellationToken).ConfigureAwait(false);
        }

        exception?.Rethrow();
    }

    /// <summary>
    /// Writes the header record if <see cref="CsvOptions{T}.HasHeader"/> is <c>true</c>.
    /// </summary>
    private static void WriteHeaderIfNeeded<T, TValue>(
        IDematerializer<T, TValue> dematerializer,
        CsvFieldWriter<T> writer
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        if (writer.Options.HasHeader)
        {
            dematerializer.WriteHeader(writer);
            writer.WriteNewline();
        }
    }

    /// <summary>
    /// If a header record hasn't been written, writes a single newline.
    /// </summary>
    private static void EnsureTrailingNewline<T>(CsvFieldWriter<T> writer)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (!writer.Options.HasHeader)
        {
            writer.WriteNewline();
        }
    }
}
