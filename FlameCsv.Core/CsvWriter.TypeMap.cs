using CommunityToolkit.Diagnostics;
using System.IO.Pipelines;
using System.Text;
using FlameCsv.Binding;
using FlameCsv.Writing;

namespace FlameCsv;

public static partial class CsvWriter
{
    public static void Write<TValue>(
        IEnumerable<TValue> values,
        TextWriter textWriter,
        CsvTypeMap<char, TValue> typeMap,
        CsvOptions<char>? options = null)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(textWriter);
        ArgumentNullException.ThrowIfNull(typeMap);

        options ??= CsvTextOptions.Default;
        var dematerializer = typeMap.GetDematerializer(options);

        WriteCore(
            values,
            CsvFieldWriter.Create(textWriter, options),
            dematerializer);
    }

    public static Task WriteToFileAsync<TValue>(
        IEnumerable<TValue> values,
        string path,
        CsvTypeMap<byte, TValue> typeMap,
        CsvOptions<byte>? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(typeMap);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        options ??= CsvUtf8Options.Default;
        var dematerializer = typeMap.GetDematerializer(options);

        return WriteAsyncCore(
            values,
            CsvFieldWriter.Create(File.OpenWrite(path), options),
            dematerializer,
            cancellationToken);
    }

    public static Task WriteToFileAsync<TValue>(
        IEnumerable<TValue> values,
        string path,
        CsvTypeMap<char, TValue> typeMap,
        CsvOptions<char>? options = null,
        Encoding? encoding = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(typeMap);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        options ??= CsvTextOptions.Default;
        var dematerializer = typeMap.GetDematerializer(options);

        return WriteAsyncCore(
            values,
            CsvFieldWriter.Create(
                new StreamWriter(File.OpenWrite(path), encoding: encoding, leaveOpen: false),
                options),
            dematerializer,
            cancellationToken);
    }

    public static Task WriteAsync<TValue>(
        IEnumerable<TValue> values,
        PipeWriter pipeWriter,
        CsvTypeMap<byte, TValue> typeMap,
        CsvOptions<byte>? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(pipeWriter);
        ArgumentNullException.ThrowIfNull(typeMap);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        options ??= CsvUtf8Options.Default;
        var dematerializer = typeMap.GetDematerializer(options);

        return WriteAsyncCore(
            values,
            CsvFieldWriter.Create(pipeWriter, options),
            dematerializer,
            cancellationToken);
    }

    public static Task WriteAsync<TValue>(
        IEnumerable<TValue> values,
        Stream stream,
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

        options ??= CsvUtf8Options.Default;
        var dematerializer = typeMap.GetDematerializer(options);

        return WriteAsyncCore(
            values,
            CsvFieldWriter.Create(stream, options, bufferSize, leaveOpen),
            dematerializer,
            cancellationToken);
    }

    public static Task WriteAsync<TValue>(
        IEnumerable<TValue> values,
        TextWriter textWriter,
        CsvTypeMap<char, TValue> typeMap,
        CsvOptions<char>? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(textWriter);
        ArgumentNullException.ThrowIfNull(typeMap);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        options ??= CsvTextOptions.Default;
        var dematerializer = typeMap.GetDematerializer(options);

        return WriteAsyncCore(
            values,
            CsvFieldWriter.Create(textWriter, options),
            dematerializer,
            cancellationToken);
    }

    /// <summary>
    /// Writes the CSV records to a string.
    /// </summary>
    /// <param name="initialCapacity">Initial capacity of the string builder</param>
    /// <returns>A <see cref="StringBuilder"/> containing the CSV</returns>
    public static StringBuilder WriteToString<TValue>(
        IEnumerable<TValue> values,
        CsvTypeMap<char, TValue> typeMap,
        CsvOptions<char>? options = null,
        int initialCapacity = 1024)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(typeMap);
        ArgumentOutOfRangeException.ThrowIfNegative(initialCapacity);

        options ??= CsvTextOptions.Default;
        var dematerializer = typeMap.GetDematerializer(options);

        var sb = new StringBuilder(capacity: initialCapacity);
        WriteCore(
            values,
            CsvFieldWriter.Create(new StringWriter(sb), options),
            dematerializer);
        return sb;
    }
}
