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
        in CsvIOOptions ioOptions = default)
    {
        try
        {
            return new CsvFieldWriter<char>(
                new TextBufferWriter(textWriter, options.Allocator, in ioOptions),
                options);
        }
        catch
        {
            if (!ioOptions.LeaveOpen) textWriter.Dispose();
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
        in CsvIOOptions ioOptions = default)
    {
        try
        {
            return new CsvFieldWriter<byte>(
                new StreamBufferWriter(stream, options.Allocator, in ioOptions),
                options);
        }
        catch
        {
            if (!ioOptions.LeaveOpen) stream.Dispose();
            throw;
        }
    }
}
