using CommunityToolkit.Diagnostics;
using FlameCsv.Runtime;
using FlameCsv.Writing;
using System.IO.Pipelines;
using System.Text;
using DAM = System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute;
using RUF = System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute;

namespace FlameCsv;

static partial class CsvWriter
{
    public static CsvWriter<char> Create(
        TextWriter textWriter,
        CsvOptions<char>? options = null,
        bool autoFlush = false)
    {
        ArgumentNullException.ThrowIfNull(textWriter);
        options ??= CsvTextOptions.Default;
        return new CsvWriterImpl<char, CsvCharBufferWriter>(
            options,
            CsvFieldWriter.Create(textWriter, options),
            autoFlush);
    }

    public static CsvWriter<byte> Create(
        Stream stream,
        CsvOptions<byte>? options = null,
        bool autoFlush = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Guard.CanWrite(stream);

        options ??= CsvUtf8Options.Default;
        return new CsvWriterImpl<byte, CsvByteBufferWriter>(
            options,
            CsvFieldWriter.Create(stream, options),
            autoFlush);
    }

    public static CsvWriter<byte> Create(
        PipeWriter pipeWriter,
        CsvOptions<byte>? options = null,
        bool autoFlush = false)
    {
        ArgumentNullException.ThrowIfNull(pipeWriter);

        options ??= CsvUtf8Options.Default;
        return new CsvWriterImpl<byte, CsvByteBufferWriter>(
            options,
            CsvFieldWriter.Create(pipeWriter, options),
            autoFlush);
    }

    [RUF(Messages.CompiledExpressions)]
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

        options ??= CsvUtf8Options.Default;
        var dematerializer = ReflectionDematerializer.Create<byte, TValue>(options);

        return WriteAsyncCore(
            values,
            CsvFieldWriter.Create(File.OpenWrite(path), options),
            dematerializer,
            cancellationToken);
    }

    [RUF(Messages.CompiledExpressions)]
    public static Task WriteToFileAsync<[DAM(Messages.ReflectionBound)] TValue>(
        IEnumerable<TValue> values,
        string path,
        CsvOptions<char>? options = null,
        Encoding? encoding = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        options ??= CsvTextOptions.Default;
        var dematerializer = ReflectionDematerializer.Create<char, TValue>(options);

        return WriteAsyncCore(
            values,
            CsvFieldWriter.Create(
                new StreamWriter(File.OpenWrite(path), encoding: encoding, leaveOpen: false),
                options),
            dematerializer,
            cancellationToken);
    }

    [RUF(Messages.CompiledExpressions)]
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

        options ??= CsvUtf8Options.Default;
        var dematerializer = ReflectionDematerializer.Create<byte, TValue>(options);

        return WriteAsyncCore(
            values,
            CsvFieldWriter.Create(pipeWriter, options),
            dematerializer,
            cancellationToken);
    }

    [RUF(Messages.CompiledExpressions)]
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

        options ??= CsvUtf8Options.Default;
        var dematerializer = ReflectionDematerializer.Create<byte, TValue>(options);

        return WriteAsyncCore(
            values,
            CsvFieldWriter.Create(stream, options, bufferSize, leaveOpen),
            dematerializer,
            cancellationToken);
    }

    [RUF(Messages.CompiledExpressions)]
    public static Task WriteAsync<[DAM(Messages.ReflectionBound)] TValue>(
        IEnumerable<TValue> values,
        TextWriter textWriter,
        CsvOptions<char>? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(textWriter);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        options ??= CsvTextOptions.Default;
        var dematerializer = ReflectionDematerializer.Create<char, TValue>(options);

        return WriteAsyncCore(
            values,
            CsvFieldWriter.Create(textWriter, options),
            dematerializer,
            cancellationToken);
    }

    [RUF(Messages.CompiledExpressions)]
    public static void Write<[DAM(Messages.ReflectionBound)] TValue>(
        IEnumerable<TValue> values,
        TextWriter textWriter,
        CsvOptions<char>? options = null)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(textWriter);

        options ??= CsvTextOptions.Default;
        var dematerializer = ReflectionDematerializer.Create<char, TValue>(options);

        WriteCore(
            values,
            CsvFieldWriter.Create(textWriter, options),
            dematerializer);
    }

    /// <summary>
    /// Writes the CSV records to a string.
    /// </summary>
    /// <param name="initialCapacity">Initial capacity of the string builder</param>
    /// <returns>A <see cref="StringBuilder"/> containing the CSV</returns>
    [RUF(Messages.CompiledExpressions)]
    public static StringBuilder WriteToString<[DAM(Messages.ReflectionBound)] TValue>(
        IEnumerable<TValue> values,
        CsvOptions<char>? options = null,
        int initialCapacity = 1024)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentOutOfRangeException.ThrowIfNegative(initialCapacity);

        options ??= CsvTextOptions.Default;
        var dematerializer = ReflectionDematerializer.Create<char, TValue>(options);

        var sb = new StringBuilder(capacity: initialCapacity);
        WriteCore(
            values,
            CsvFieldWriter.Create(new StringWriter(sb), options),
            dematerializer);
        return sb;
    }

    private static async Task WriteAsyncCore<T, TWriter, TValue>(
        IEnumerable<TValue> values,
        CsvFieldWriter<T, TWriter> writer,
        IDematerializer<T, TValue> dematerializer,
        CancellationToken cancellationToken)
        where T : unmanaged, IEquatable<T>
        where TWriter : struct, ICsvBufferWriter<T>
    {
        Exception? exception = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (writer.WriteHeader)
                dematerializer.WriteHeader(writer);

            foreach (var value in values)
            {
                if (writer.Writer.NeedsFlush)
                    await writer.Writer.FlushAsync(cancellationToken).ConfigureAwait(false);

                dematerializer.Write(writer, value);
            }
        }
        catch (Exception e)
        {
            // store exception so the writer knows not to flush when disposing
            exception = e;
        }
        finally
        {
            // Don't flush if canceled
            if (exception is null && cancellationToken.IsCancellationRequested)
                exception = new OperationCanceledException(cancellationToken);

            // this re-throws possible exceptions after disposing its internals
            await writer.Writer.CompleteAsync(exception, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void WriteCore<T, TWriter, TValue>(
        IEnumerable<TValue> values,
        CsvFieldWriter<T, TWriter> writer,
        IDematerializer<T, TValue> dematerializer)
        where T : unmanaged, IEquatable<T>
        where TWriter : struct, ICsvBufferWriter<T>
    {
        Exception? exception = null;

        try
        {
            if (writer.WriteHeader)
                dematerializer.WriteHeader(writer);

            foreach (var value in values)
            {
                if (writer.Writer.NeedsFlush)
                    writer.Writer.Flush();

                dematerializer.Write(writer, value);
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
