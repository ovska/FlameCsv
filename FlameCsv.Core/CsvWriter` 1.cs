using System.IO.Pipelines;
using FlameCsv.Binding;
using FlameCsv.Writing;
using FlameCsv.Extensions;

namespace FlameCsv;

static partial class CsvWriter
{
    /// <summary>
    /// Returns a writer instance that can be used to write custom fields, multiple different types,
    /// or multiple CSV documents into the same output.<br/>
    /// The writer should be used with <see langword="using"/>, or manually disposed after use.
    /// </summary>
    /// <param name="textWriter">Writer to write the CSV to</param>
    /// <param name="options">Options that define the dialect and converters used</param>
    /// <param name="autoFlush">
    /// Whether to flush the writer when the internal buffer is starting to fill up. Disposing the writer flushes any remaining data.
    /// </param>
    /// <returns>Writer instance</returns>
    public static CsvWriter<char> Create(
        TextWriter textWriter,
        CsvOptions<char>? options = null,
        bool autoFlush = false)
    {
        ArgumentNullException.ThrowIfNull(textWriter);
        options ??= CsvOptions<char>.Default;
        return new CsvWriterImpl<char>(
            options,
            CsvFieldWriter.Create(textWriter, options, 4096),
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
        return new CsvWriterImpl<byte>(
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
        return new CsvWriterImpl<byte>(
            options,
            CsvFieldWriter.Create(pipeWriter, options),
            autoFlush);
    }
}

public abstract class CsvWriter<T> : IDisposable, IAsyncDisposable where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Amount of fields written to the current record (starting from 0). If not zero, delimiter is written before any more data.
    /// </summary>
    public abstract int ColumnIndex { get; }

    /// <summary>
    /// Current line in the CSV (starting from 1).
    /// </summary>
    public abstract int LineIndex { get; }

    internal CsvWriter()
    {
    }

    [RUF(Messages.CompiledExpressions), RDC(Messages.CompiledExpressions)] public abstract void WriteHeader<[DAM(Messages.ReflectionBound)] TRecord>();
    public abstract void WriteHeader<TRecord>(CsvTypeMap<T, TRecord> typeMap);
    [RUF(Messages.CompiledExpressions), RDC(Messages.CompiledExpressions)] public abstract ValueTask WriteHeaderAsync<[DAM(Messages.ReflectionBound)] TRecord>(CancellationToken cancellationToken = default);
    public abstract ValueTask WriteHeaderAsync<TRecord>(CsvTypeMap<T, TRecord> typeMap, CancellationToken cancellationToken = default);

    [RUF(Messages.CompiledExpressions), RDC(Messages.CompiledExpressions)] public abstract void WriteRecord<[DAM(Messages.ReflectionBound)] TRecord>(TRecord value);
    public abstract void WriteRecord<TRecord>(CsvTypeMap<T, TRecord> typeMap, TRecord value);
    [RUF(Messages.CompiledExpressions), RDC(Messages.CompiledExpressions)] public abstract ValueTask WriteRecordAsync<[DAM(Messages.ReflectionBound)] TRecord>(TRecord value, CancellationToken cancellationToken = default);
    public abstract ValueTask WriteRecordAsync<TRecord>(CsvTypeMap<T, TRecord> typeMap, TRecord value, CancellationToken cancellationToken = default);

    public abstract void WriteRaw(scoped ReadOnlySpan<T> value);
    public abstract ValueTask WriteRawAsync(scoped ReadOnlySpan<T> value, CancellationToken cancellationToken = default);
    public abstract void WriteField<TField>(TField? value);
    public abstract ValueTask WriteFieldAsync<TField>(TField? value, CancellationToken cancellationToken);
    public abstract void WriteField(ReadOnlySpan<char> text, bool skipEscaping = false);
    public abstract ValueTask WriteFieldAsync(ReadOnlySpan<char> text, bool skipEscaping = false, CancellationToken cancellationToken = default);
    public abstract void NextRecord();
    public abstract ValueTask NextRecordAsync(CancellationToken cancellationToken = default);
    public abstract void Flush();
    public abstract ValueTask FlushAsync(CancellationToken cancellationToken = default);

    public abstract void Dispose();
    public abstract ValueTask DisposeAsync();
}
