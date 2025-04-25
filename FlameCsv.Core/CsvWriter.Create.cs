using System.IO.Pipelines;
using FlameCsv.Writing;
using FlameCsv.Extensions;

namespace FlameCsv;

static partial class CsvWriter
{
    /// <summary>
    /// Returns a writer instance that can be used to write custom fields, multiple different types,
    /// or multiple CSV documents into the same output.<br/>
    /// After use, the writer should be disposed, or completed with <see cref="CsvWriter{T}.Complete"/> or
    /// <see cref="CsvAsyncWriter{T}.CompleteAsync"/>.
    /// </summary>
    /// <param name="textWriter">Writer to write the CSV to</param>
    /// <param name="options">Options instance. If null, <see cref="CsvOptions{T}.Default"/> is used</param>
    /// <param name="autoFlush">Whether to automatically flush after </param>
    /// <returns>Writer instance</returns>
    public static CsvWriter<char> Create(
        TextWriter textWriter,
        CsvOptions<char>? options = null,
        bool autoFlush = false)
    {
        ArgumentNullException.ThrowIfNull(textWriter);
        options ??= CsvOptions<char>.Default;
        return new CsvWriter<char>(
            CsvFieldWriter.Create(textWriter, options, DefaultFileStreamBufferSize, false),
            autoFlush);
    }

    /// <summary><inheritdoc cref="Create(TextWriter, CsvOptions{char}?, bool)" path="/summary"/></summary>
    /// <param name="stream">Stream to write the CSV to</param>
    /// <param name="options">Options instance. If null, <see cref="CsvOptions{T}.Default"/> is used</param>
    /// <param name="autoFlush">Whether to automatically flush after </param>
    /// <returns>Writer instance</returns>
    public static CsvWriter<byte> Create(
        Stream stream,
        CsvOptions<byte>? options = null,
        bool autoFlush = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Guard.CanWrite(stream);

        options ??= CsvOptions<byte>.Default;
        return new CsvWriter<byte>(
            CsvFieldWriter.Create(stream, options),
            autoFlush);
    }

    /// <summary><inheritdoc cref="Create(TextWriter, CsvOptions{char}?, bool)" path="/summary"/></summary>
    /// <remarks>
    /// Writing to a pipe does not support synchronous flushing.
    /// </remarks>
    /// <param name="pipeWriter">Pipe to write the CSV to</param>
    /// <param name="options">Options instance. If null, <see cref="CsvOptions{T}.Default"/> is used</param>
    /// <param name="autoFlush">Whether to automatically flush after </param>
    /// <returns>Writer instance</returns>
    public static CsvAsyncWriter<byte> Create(
        PipeWriter pipeWriter,
        CsvOptions<byte>? options = null,
        bool autoFlush = false)
    {
        ArgumentNullException.ThrowIfNull(pipeWriter);

        options ??= CsvOptions<byte>.Default;
        return new CsvAsyncWriter<byte>(
            CsvFieldWriter.Create(pipeWriter, options),
            autoFlush);
    }
}
