using System.IO.Pipelines;
using System.Text;
using FlameCsv.Extensions;
using FlameCsv.Writing;

namespace FlameCsv;

static partial class CsvWriter
{
    [RUF(Messages.CompiledExpressions), RDC(Messages.CompiledExpressions)]
    public static Task WriteToFileAsync<[DAM(Messages.ReflectionBound)] TValue>(
        string path,
        IEnumerable<TValue> values,
        CsvOptions<byte>? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        FileStream? stream = null;

        try
        {
            stream = File.OpenWrite(path);
            options ??= CsvOptions<byte>.Default;
            IDematerializer<byte, TValue> dematerializer = options.TypeBinder.GetDematerializer<TValue>();
            return WriteAsyncCore(
                values,
                CsvFieldWriter.Create(stream, options),
                dematerializer,
                cancellationToken);
        }
        catch
        {
            // ensure the stream is disposed if an exception is thrown before the task is returned
            stream?.Dispose();
            throw;
        }
    }

    [RUF(Messages.CompiledExpressions), RDC(Messages.CompiledExpressions)]
    public static Task WriteToFileAsync<[DAM(Messages.ReflectionBound)] TValue>(
        string path,
        IEnumerable<TValue> values,
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
        var dematerializer = options.TypeBinder.GetDematerializer<TValue>();

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
        PipeWriter pipeWriter,
        IEnumerable<TValue> values,
        CsvOptions<byte>? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(pipeWriter);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        options ??= CsvOptions<byte>.Default;
        var dematerializer = options.TypeBinder.GetDematerializer<TValue>();

        return WriteAsyncCore(
            values,
            CsvFieldWriter.Create(pipeWriter, options),
            dematerializer,
            cancellationToken);
    }

    [RUF(Messages.CompiledExpressions), RDC(Messages.CompiledExpressions)]
    public static Task WriteAsync<[DAM(Messages.ReflectionBound)] TValue>(
        Stream stream,
        IEnumerable<TValue> values,
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
        var dematerializer = options.TypeBinder.GetDematerializer<TValue>();

        return WriteAsyncCore(
            values,
            CsvFieldWriter.Create(stream, options, bufferSize, leaveOpen),
            dematerializer,
            cancellationToken);
    }

    [RUF(Messages.CompiledExpressions), RDC(Messages.CompiledExpressions)]
    public static Task WriteAsync<[DAM(Messages.ReflectionBound)] TValue>(
        TextWriter textWriter,
        IEnumerable<TValue> values,
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
        var dematerializer = options.TypeBinder.GetDematerializer<TValue>();

        return WriteAsyncCore(
            values,
            CsvFieldWriter.Create(textWriter, options, bufferSize),
            dematerializer,
            cancellationToken);
    }

    [RUF(Messages.CompiledExpressions), RDC(Messages.CompiledExpressions)]
    public static void Write<[DAM(Messages.ReflectionBound)] TValue>(
        TextWriter textWriter,
        IEnumerable<TValue> values,
        CsvOptions<char>? options = null,
        int bufferSize = -1)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(textWriter);

        if (bufferSize != -1)
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);

        options ??= CsvOptions<char>.Default;
        var dematerializer = options.TypeBinder.GetDematerializer<TValue>();

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
    /// <returns>
    /// <see cref="StringBuilder"/> containing the CSV (same instance as <paramref name="builder"/> if provided)
    /// </returns>
    [RUF(Messages.CompiledExpressions), RDC(Messages.CompiledExpressions)]
    public static StringBuilder WriteToString<[DAM(Messages.ReflectionBound)] TValue>(
        IEnumerable<TValue> values,
        CsvOptions<char>? options = null,
        StringBuilder? builder = null)
    {
        ArgumentNullException.ThrowIfNull(values);

        options ??= CsvOptions<char>.Default;
        var dematerializer = options.TypeBinder.GetDematerializer<TValue>();

        builder ??= new();
        WriteCore(
            values,
            CsvFieldWriter.Create(new StringWriter(builder), options, bufferSize: DefaultBufferSize),
            dematerializer);
        return builder;
    }
}
