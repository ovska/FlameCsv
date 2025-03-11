using System.IO.Pipelines;
using FlameCsv.IO;
using JetBrains.Annotations;

namespace FlameCsv.Writing;

internal static class CsvFieldWriter
{
    [MustDisposeResource]
    public static CsvFieldWriter<char> Create(
        TextWriter textWriter,
        CsvOptions<char> options,
        int bufferSize,
        bool leaveOpen)
    {
        try
        {
            return new CsvFieldWriter<char>(
                new CsvCharPipeWriter(textWriter, options.Allocator, bufferSize, leaveOpen),
                options);
        }
        catch
        {
            if (!leaveOpen) textWriter.Dispose();
            throw;
        }
    }

    [MustDisposeResource]
    public static CsvFieldWriter<byte> Create(
        PipeWriter pipeWriter,
        CsvOptions<byte> options)
    {
        try
        {
            return new CsvFieldWriter<byte>(
                new PipeBufferWriter(pipeWriter),
                options);
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
        int bufferSize = -1,
        bool leaveOpen = false)
    {
        try
        {
            return new CsvFieldWriter<byte>(
                new CsvStreamPipeWriter(stream, options.Allocator, bufferSize, leaveOpen),
                options);
        }
        catch
        {
            if (!leaveOpen) stream.Dispose();
            throw;
        }
    }
}
