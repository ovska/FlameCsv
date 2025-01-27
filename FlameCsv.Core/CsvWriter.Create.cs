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
    /// <see cref="CsvWriter{T}.CompleteAsync"/>.
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
            options,
            CsvFieldWriter.Create(textWriter, options, DefaultBufferSize),
            autoFlush);
    }

    /// <inheritdoc cref="Create(TextWriter, CsvOptions{char}?, bool)"/>
    /// <remarks>
    /// Writing to <see cref="Stream"/> does not support synchronous flushing. <see langword="await"/>
    /// <see langword="using"/> must be used, and either <paramref name="autoFlush"/> <see langword="false"/> or
    /// async-overloads of the write methods such as <see cref="CsvWriter{T}.WriteRecordAsync{TRecord}(TRecord, CancellationToken)"/>.
    /// </remarks>
    /// <param name="stream">Stream to write the CSV to</param>
    /// <param name="options"></param>
    /// <param name="autoFlush">Automatically flush if needed after each write operation</param>
    public static CsvWriter<byte> Create(
        Stream stream,
        CsvOptions<byte>? options = null,
        bool autoFlush = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Guard.CanWrite(stream);

        options ??= CsvOptions<byte>.Default;
        return new CsvWriter<byte>(
            options,
            CsvFieldWriter.Create(stream, options),
            autoFlush);
    }

    /// <inheritdoc cref="Create(TextWriter, CsvOptions{char}?, bool)"/>
    /// <remarks>
    /// Writing to <see cref="PipeWriter"/> does not support synchronous flushing. <see langword="await"/>
    /// <see langword="using"/> must be used, and either <paramref name="autoFlush"/> <see langword="false"/> or
    /// async-overloads of the write methods such as <see cref="CsvWriter{T}.WriteRecordAsync{TRecord}(TRecord, CancellationToken)"/>.
    /// </remarks>
    /// <param name="pipeWriter">Writer to write the CSV to</param>
    /// <param name="options"></param>
    /// <param name="autoFlush">Automatically flush if needed after each write operation</param>
    public static CsvWriter<byte> Create(
        PipeWriter pipeWriter,
        CsvOptions<byte>? options = null,
        bool autoFlush = false)
    {
        ArgumentNullException.ThrowIfNull(pipeWriter);

        options ??= CsvOptions<byte>.Default;
        return new CsvWriter<byte>(
            options,
            CsvFieldWriter.Create(pipeWriter, options),
            autoFlush);
    }
}
