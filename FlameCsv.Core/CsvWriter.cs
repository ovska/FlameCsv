using FlameCsv.Runtime;
using FlameCsv.Writing;
using System.IO.Pipelines;
using System.Text;
using FlameCsv.Extensions;

namespace FlameCsv;

static partial class CsvWriter
{
    [RUF(Messages.CompiledExpressions), RDC(Messages.CompiledExpressions)]
    public static Task WriteToFileAsync<[DAM(Messages.ReflectionBound)] TValue>(
        IEnumerable<TValue> values,
        string path,
        CsvOptions<byte>? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        options ??= CsvOptions<byte>.Default;
        var dematerializer = ReflectionDematerializer.Create<byte, TValue>(options);

        return WriteAsyncCore(
            values,
            CsvFieldWriter.Create(File.OpenWrite(path), options),
            dematerializer,
            cancellationToken);
    }

    [RUF(Messages.CompiledExpressions), RDC(Messages.CompiledExpressions)]
    public static Task WriteToFileAsync<[DAM(Messages.ReflectionBound)] TValue>(
        IEnumerable<TValue> values,
        string path,
        CsvOptions<char>? options = null,
        Encoding? encoding = null,
        int bufferSize = -1,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (bufferSize != -1)
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        options ??= CsvOptions<char>.Default;
        var dematerializer = ReflectionDematerializer.Create<char, TValue>(options);

        return WriteAsyncCore(
            values,
            CsvFieldWriter.Create(
                new StreamWriter(File.OpenWrite(path), encoding: encoding, leaveOpen: false),
                options,
                bufferSize: bufferSize),
            dematerializer,
            cancellationToken);
    }

    [RUF(Messages.CompiledExpressions), RDC(Messages.CompiledExpressions)]
    public static Task WriteAsync<[DAM(Messages.ReflectionBound)] TValue>(
        IEnumerable<TValue> values,
        PipeWriter pipeWriter,
        CsvOptions<byte>? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(pipeWriter);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        options ??= CsvOptions<byte>.Default;
        var dematerializer = ReflectionDematerializer.Create<byte, TValue>(options);

        return WriteAsyncCore(
            values,
            CsvFieldWriter.Create(pipeWriter, options),
            dematerializer,
            cancellationToken);
    }

    [RUF(Messages.CompiledExpressions), RDC(Messages.CompiledExpressions)]
    public static Task WriteAsync<[DAM(Messages.ReflectionBound)] TValue>(
        IEnumerable<TValue> values,
        Stream stream,
        CsvOptions<byte>? options = null,
        int bufferSize = -1,
        bool leaveOpen = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(stream);
        Guard.CanWrite(stream);

        if (bufferSize != -1)
            ArgumentOutOfRangeException.ThrowIfLessThan(bufferSize, 1);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        options ??= CsvOptions<byte>.Default;
        var dematerializer = ReflectionDematerializer.Create<byte, TValue>(options);

        return WriteAsyncCore(
            values,
            CsvFieldWriter.Create(stream, options, bufferSize, leaveOpen),
            dematerializer,
            cancellationToken);
    }

    [RUF(Messages.CompiledExpressions), RDC(Messages.CompiledExpressions)]
    public static Task WriteAsync<[DAM(Messages.ReflectionBound)] TValue>(
        IEnumerable<TValue> values,
        TextWriter textWriter,
        CsvOptions<char>? options = null,
        int bufferSize = -1,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(textWriter);

        if (bufferSize != -1)
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        options ??= CsvOptions<char>.Default;
        var dematerializer = ReflectionDematerializer.Create<char, TValue>(options);

        return WriteAsyncCore(
            values,
            CsvFieldWriter.Create(textWriter, options, bufferSize),
            dematerializer,
            cancellationToken);
    }

    [RUF(Messages.CompiledExpressions), RDC(Messages.CompiledExpressions)]
    public static void Write<[DAM(Messages.ReflectionBound)] TValue>(
        IEnumerable<TValue> values,
        TextWriter textWriter,
        CsvOptions<char>? options = null,
        int bufferSize = -1)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(textWriter);

        if (bufferSize != -1)
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);

        options ??= CsvOptions<char>.Default;
        var dematerializer = ReflectionDematerializer.Create<char, TValue>(options);

        WriteCore(
            values,
            CsvFieldWriter.Create(textWriter, options, bufferSize),
            dematerializer);
    }

    /// <summary>
    /// Writes the CSV records to a string.
    /// </summary>
    /// <param name="values">Values to write to the string builder</param>
    /// <param name="options">Optional user configured options to use</param>
    /// <param name="builder">Optional builder to write the CSV to.</param>
    /// <returns><see cref="StringBuilder"/> containing the CSV</returns>
    [RUF(Messages.CompiledExpressions), RDC(Messages.CompiledExpressions)]
    public static StringBuilder WriteToString<[DAM(Messages.ReflectionBound)] TValue>(
        IEnumerable<TValue> values,
        CsvOptions<char>? options = null,
        StringBuilder? builder = null)
    {
        ArgumentNullException.ThrowIfNull(values);

        options ??= CsvOptions<char>.Default;
        var dematerializer = ReflectionDematerializer.Create<char, TValue>(options);

        var sb = builder ?? new StringBuilder(capacity: 4096);
        WriteCore(
            values,
            CsvFieldWriter.Create(new StringWriter(sb), options, bufferSize: 4096),
            dematerializer);
        return sb;
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

            writer.TryWriteHeader(dematerializer);

            foreach (var value in values)
            {
                if (writer.Writer.NeedsFlush)
                    await writer.Writer.FlushAsync(cancellationToken).ConfigureAwait(false);

                dematerializer.Write(in writer, value);
            }
        }
        catch (Exception e)
        {
            // store exception so the writer knows not to flush when disposing
            exception = e;
        }
        finally
        {
            // Don't flush if cancelled
            if (exception is null && cancellationToken.IsCancellationRequested)
                exception = new OperationCanceledException(cancellationToken);

            // this re-throws possible exceptions after disposing its internals
            await writer.Writer.CompleteAsync(exception, cancellationToken).ConfigureAwait(false);
        }
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
            writer.TryWriteHeader(dematerializer);

            foreach (var value in values)
            {
                if (writer.Writer.NeedsFlush)
                    writer.Writer.Flush();

                dematerializer.Write(in writer, value);
            }
        }
        catch (Exception e)
        {
            // store exception so the writer knows not to flush when disposing
            exception = e;
        }
        finally
        {
            // this re-throws possible exceptions after disposing its internals
            writer.Writer.Complete(exception);
        }
    }
}
