using System.IO.Pipelines;
using FlameCsv.IO;
using FlameCsv.IO.Internal;
using JetBrains.Annotations;

namespace FlameCsv.Writing;

internal static class CsvFieldWriter
{
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
