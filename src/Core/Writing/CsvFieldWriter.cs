using System.IO.Pipelines;
using FlameCsv.IO;
using FlameCsv.IO.Internal;
using JetBrains.Annotations;

namespace FlameCsv.Writing;

/// <summary>
/// Provides factory methods for creating <see cref="CsvFieldWriter{T}"/> instances.<br/>
/// The <see cref="CsvFieldWriter{T}"/> type should only be used for maximum performance,
/// as it allows you to sidestep various conveniences and guardrails present in <see cref="CsvWriter{T}"/>.
/// </summary>
public static class CsvFieldWriter
{
    /// <summary>
    /// Creates a new instance that writes to the specified <see cref="ICsvBufferWriter{T}"/>.
    /// </summary>
    /// <remarks>
    /// The writer is completed if an exception occurs during construction.
    /// </remarks>
    [MustDisposeResource]
    public static CsvFieldWriter<T> Create<T>(ICsvBufferWriter<T> writer, CsvOptions<T> options)
        where T : unmanaged, IBinaryInteger<T>
    {
        try
        {
            return new CsvFieldWriter<T>(writer, options);
        }
        catch (Exception ex)
        {
            writer.Complete(ex);
            throw;
        }
    }

    /// <summary>
    /// Creates a new instance that writes to the specified <see cref="TextWriter"/>.
    /// </summary>
    /// <remarks>
    /// The writer is disposed if an exception occurs during construction and <see cref="CsvIOOptions.LeaveOpen"/> is <c>false</c>.
    /// </remarks>
    [MustDisposeResource]
    public static CsvFieldWriter<char> Create(
        TextWriter textWriter,
        CsvOptions<char> options,
        in CsvIOOptions ioOptions = default
    )
    {
        try
        {
            return new CsvFieldWriter<char>(new TextBufferWriter(textWriter, options.Allocator, in ioOptions), options);
        }
        catch
        {
            if (!ioOptions.LeaveOpen)
                textWriter.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Creates a new instance that writes to the specified <see cref="PipeWriter"/>.
    /// </summary>
    /// <remarks>
    /// The writer is completed if an exception occurs during construction.
    /// </remarks>
    [MustDisposeResource]
    public static CsvFieldWriter<byte> Create(PipeWriter pipeWriter, CsvOptions<byte> options)
    {
        try
        {
            return new CsvFieldWriter<byte>(new PipeBufferWriter(pipeWriter), options);
        }
        catch (Exception e)
        {
            pipeWriter.Complete(e);
            throw;
        }
    }

    /// <summary>
    /// Creates a new instance that writes to the specified <see cref="Stream"/>.
    /// </summary>
    /// <remarks>
    /// The stream is disposed if an exception occurs during construction and <see cref="CsvIOOptions.LeaveOpen"/> is <c>false</c>.
    /// </remarks>
    [MustDisposeResource]
    public static CsvFieldWriter<byte> Create(
        Stream stream,
        CsvOptions<byte> options,
        in CsvIOOptions ioOptions = default
    )
    {
        try
        {
            return new CsvFieldWriter<byte>(new StreamBufferWriter(stream, options.Allocator, in ioOptions), options);
        }
        catch
        {
            if (!ioOptions.LeaveOpen)
                stream.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Creates a new instance that writes to the specified <see cref="ICsvBufferWriter{T}"/>.
    /// </summary>
    /// <remarks>
    /// The writer is completed asynchronously if an exception occurs during construction.
    /// </remarks>
    [MustDisposeResource]
    public static async ValueTask<CsvFieldWriter<T>> CreateAsync<T>(ICsvBufferWriter<T> writer, CsvOptions<T> options)
        where T : unmanaged, IBinaryInteger<T>
    {
        try
        {
            return new CsvFieldWriter<T>(writer, options);
        }
        catch (Exception ex)
        {
            await writer.CompleteAsync(ex).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Creates a new instance that writes to the specified <see cref="TextWriter"/>.
    /// </summary>
    /// <remarks>
    /// The writer is disposed asynchronously if an exception occurs during construction and <see cref="CsvIOOptions.LeaveOpen"/> is <c>false</c>.
    /// </remarks>
    [MustDisposeResource]
    public static async ValueTask<CsvFieldWriter<char>> CreateAsync(
        TextWriter textWriter,
        CsvOptions<char> options,
        CsvIOOptions ioOptions = default
    )
    {
        try
        {
            return new CsvFieldWriter<char>(new TextBufferWriter(textWriter, options.Allocator, in ioOptions), options);
        }
        catch
        {
            if (!ioOptions.LeaveOpen)
                await textWriter.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Creates a new instance that writes to the specified <see cref="Stream"/>.
    /// </summary>
    /// <remarks>
    /// The stream is disposed asynchronously if an exception occurs during construction and <see cref="CsvIOOptions.LeaveOpen"/> is <c>false</c>.
    /// </remarks>
    [MustDisposeResource]
    public static async ValueTask<CsvFieldWriter<byte>> CreateAsync(
        Stream stream,
        CsvOptions<byte> options,
        CsvIOOptions ioOptions = default
    )
    {
        try
        {
            return new CsvFieldWriter<byte>(new StreamBufferWriter(stream, options.Allocator, in ioOptions), options);
        }
        catch
        {
            if (!ioOptions.LeaveOpen)
                await stream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Creates a new instance that writes to the specified <see cref="PipeWriter"/>.
    /// </summary>
    /// <remarks>
    /// The writer is completed asynchronously if an exception occurs during construction.
    /// </remarks>
    [MustDisposeResource]
    public static async ValueTask<CsvFieldWriter<byte>> CreateAsync(PipeWriter pipeWriter, CsvOptions<byte> options)
    {
        try
        {
            return new CsvFieldWriter<byte>(new PipeBufferWriter(pipeWriter), options);
        }
        catch (Exception e)
        {
            await pipeWriter.CompleteAsync(e).ConfigureAwait(false);
            throw;
        }
    }
}
