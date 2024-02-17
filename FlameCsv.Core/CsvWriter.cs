using CommunityToolkit.Diagnostics;
using FlameCsv.Runtime;
using FlameCsv.Writing;
using System.IO.Pipelines;
using System.Text;
using DAM = System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute;
using RUF = System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute;

namespace FlameCsv;

public static partial class CsvWriter
{
    [RUF(Messages.CompiledExpressions)]
    public static Task WriteToFileAsync<[DAM(Messages.ReflectionBound)] TValue>(
        IEnumerable<TValue> values,
        string path,
        CsvOptions<byte> options,
        CsvContextOverride<byte> context = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(options);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        var _context = new CsvWritingContext<byte>(options, in context);
        var dematerializer = ReflectionDematerializer.Create<byte, TValue>(in _context);

        return WriteAsyncCore(
            values,
            CsvFieldWriter.Create(File.OpenWrite(path), in _context),
            dematerializer,
            cancellationToken);
    }

    [RUF(Messages.CompiledExpressions)]
    public static Task WriteToFileAsync<[DAM(Messages.ReflectionBound)] TValue>(
        IEnumerable<TValue> values,
        string path,
        CsvOptions<char> options,
        Encoding? encoding = null,
        CsvContextOverride<char> context = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(options);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        var _context = new CsvWritingContext<char>(options, in context);
        var dematerializer = ReflectionDematerializer.Create<char, TValue>(in _context);

        return WriteAsyncCore(
            values,
            CsvFieldWriter.Create(
                new StreamWriter(File.OpenWrite(path), encoding: encoding, leaveOpen: false),
                in _context),
            dematerializer,
            cancellationToken);
    }

    [RUF(Messages.CompiledExpressions)]
    public static Task WriteAsync<[DAM(Messages.ReflectionBound)] TValue>(
        IEnumerable<TValue> values,
        PipeWriter pipeWriter,
        CsvOptions<byte> options,
        CsvContextOverride<byte> context = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(pipeWriter);
        ArgumentNullException.ThrowIfNull(options);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        var _context = new CsvWritingContext<byte>(options, in context);
        var dematerializer = ReflectionDematerializer.Create<byte, TValue>(in _context);

        return WriteAsyncCore(
            values,
            CsvFieldWriter.Create(pipeWriter, in _context),
            dematerializer,
            cancellationToken);
    }

    [RUF(Messages.CompiledExpressions)]
    public static Task WriteAsync<[DAM(Messages.ReflectionBound)] TValue>(
        IEnumerable<TValue> values,
        Stream stream,
        CsvOptions<byte> options,
        CsvContextOverride<byte> context = default,
        int bufferSize = -1,
        bool leaveOpen = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);
        Guard.CanWrite(stream);

        if (bufferSize != -1)
            ArgumentOutOfRangeException.ThrowIfLessThan(bufferSize, 1);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        var _context = new CsvWritingContext<byte>(options, in context);
        var dematerializer = ReflectionDematerializer.Create<byte, TValue>(in _context);

        return WriteAsyncCore(
            values,
            CsvFieldWriter.Create(stream, in _context, bufferSize, leaveOpen),
            dematerializer,
            cancellationToken);
    }

    /// <summary>
    /// Writes the CSV records to a string.
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="values"></param>
    /// <param name="options"></param>
    /// <param name="context"></param>
    /// <param name="initialCapacity">Initial capacity of the string builder</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>A <see cref="StringBuilder"/> containing the CSV</returns>
    [RUF(Messages.CompiledExpressions)]
    public static Task<StringBuilder> WriteToStringAsync<[DAM(Messages.ReflectionBound)] TValue>(
        IEnumerable<TValue> values,
        CsvOptions<char> options,
        CsvContextOverride<char> context = default,
        int initialCapacity = 1024,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegative(initialCapacity);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<StringBuilder>(cancellationToken);

        return Core();

        async Task<StringBuilder> Core()
        {
            var _context = new CsvWritingContext<char>(options, in context);
            var dematerializer = ReflectionDematerializer.Create<char, TValue>(in _context);

            var sb = new StringBuilder(capacity: 1024);
            await WriteAsyncCore(
                values,
                CsvFieldWriter.Create(new StringWriter(sb), in _context),
                dematerializer,
                cancellationToken);
            return sb;
        }
    }

    private static async Task WriteAsyncCore<T, TWriter, TValue>(
        IEnumerable<TValue> values,
        CsvFieldWriter<T, TWriter> writer,
        IDematerializer<T, TValue> dematerializer,
        CancellationToken cancellationToken)
        where T : unmanaged, IEquatable<T>
        where TWriter : struct, IAsyncBufferWriter<T>
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
            await writer.Writer.CompleteAsync(exception, cancellationToken);
        }
    }
}
