using System.IO.Pipelines;
using System.Text;
using FlameCsv.Binding;
using FlameCsv.Extensions;
using FlameCsv.Writing;

namespace FlameCsv;

public static partial class CsvWriter
{
    public static void Write<TValue>(
        TextWriter textWriter,
        IEnumerable<TValue> values,
        CsvTypeMap<char, TValue> typeMap,
        CsvOptions<char>? options = null,
        int bufferSize = -1)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(textWriter);
        ArgumentNullException.ThrowIfNull(typeMap);
        if (bufferSize != -1)
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);

        options ??= CsvOptions<char>.Default;
        var dematerializer = typeMap.GetDematerializer(options);

        WriteCore(
            values,
            CsvFieldWriter.Create(textWriter, options, bufferSize),
            dematerializer);
    }

    public static Task WriteToFileAsync<TValue>(
        string path,
        IEnumerable<TValue> values,
        CsvTypeMap<byte, TValue> typeMap,
        CsvOptions<byte>? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(typeMap);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        Stream? stream = null;

        try
        {
            stream = File.OpenWrite(path);
            options ??= CsvOptions<byte>.Default;
            var dematerializer = typeMap.GetDematerializer(options);

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

    public static Task WriteToFileAsync<TValue>(
        string path,
        IEnumerable<TValue> values,
        CsvTypeMap<char, TValue> typeMap,
        CsvOptions<char>? options = null,
        Encoding? encoding = null,
        int bufferSize = -1,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(typeMap);
        if (bufferSize != -1)
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        Stream? stream = null;

        try
        {
            stream = File.OpenWrite(path);

            options ??= CsvOptions<char>.Default;
            var dematerializer = typeMap.GetDematerializer(options);

            return WriteAsyncCore(
                values,
                CsvFieldWriter.Create(
                    new StreamWriter(stream, encoding: encoding, leaveOpen: false),
                    options,
                    bufferSize: bufferSize),
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

    public static Task WriteAsync<TValue>(
        PipeWriter pipeWriter,
        IEnumerable<TValue> values,
        CsvTypeMap<byte, TValue> typeMap,
        CsvOptions<byte>? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(pipeWriter);
        ArgumentNullException.ThrowIfNull(typeMap);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        options ??= CsvOptions<byte>.Default;
        var dematerializer = typeMap.GetDematerializer(options);

        return WriteAsyncCore(
            values,
            CsvFieldWriter.Create(pipeWriter, options),
            dematerializer,
            cancellationToken);
    }

    public static Task WriteAsync<TValue>(
        Stream stream,
        IEnumerable<TValue> values,
        CsvTypeMap<byte, TValue> typeMap,
        CsvOptions<byte>? options = null,
        int bufferSize = -1,
        bool leaveOpen = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(typeMap);
        Guard.CanWrite(stream);

        if (bufferSize != -1)
            ArgumentOutOfRangeException.ThrowIfLessThan(bufferSize, 1);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        options ??= CsvOptions<byte>.Default;
        var dematerializer = typeMap.GetDematerializer(options);

        return WriteAsyncCore(
            values,
            CsvFieldWriter.Create(stream, options, bufferSize, leaveOpen),
            dematerializer,
            cancellationToken);
    }

    public static Task WriteAsync<TValue>(
        TextWriter textWriter,
        IEnumerable<TValue> values,
        CsvTypeMap<char, TValue> typeMap,
        CsvOptions<char>? options = null,
        int bufferSize = -1,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(textWriter);
        ArgumentNullException.ThrowIfNull(typeMap);
        if (bufferSize != -1)
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        options ??= CsvOptions<char>.Default;
        var dematerializer = typeMap.GetDematerializer(options);

        return WriteAsyncCore(
            values,
            CsvFieldWriter.Create(textWriter, options, bufferSize),
            dematerializer,
            cancellationToken);
    }

    /// <summary>
    /// Writes the CSV records to a string.
    /// </summary>
    /// <param name="values">Values to write to the string builder</param>
    /// <param name="typeMap">Type map to use for writing</param>
    /// <param name="options">Optional user configured options to use</param>
    /// <param name="builder">Optional builder to write the CSV to.</param>
    /// <returns>
    /// <see cref="StringBuilder"/> containing the CSV (same instance as <paramref name="builder"/> if provided)
    /// </returns>
    public static StringBuilder WriteToString<TValue>(
        IEnumerable<TValue> values,
        CsvTypeMap<char, TValue> typeMap,
        CsvOptions<char>? options = null,
        StringBuilder? builder = null)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(typeMap);

        options ??= CsvOptions<char>.Default;
        var dematerializer = typeMap.GetDematerializer(options);

        builder ??= new();
        WriteCore(
            values,
            CsvFieldWriter.Create(new StringWriter(builder), options, bufferSize: DefaultBufferSize),
            dematerializer);
        return builder;
    }
}
