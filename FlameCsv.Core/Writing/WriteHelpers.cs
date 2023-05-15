using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;

namespace FlameCsv.Writing;

internal static class WriteHelpers
{
    public static CsvRecordWriter<char, CsvCharBufferWriter> Create(TextWriter textWriter, CsvOptions<char> options)
    {
        return new CsvRecordWriter<char, CsvCharBufferWriter>(
            new CsvCharBufferWriter(textWriter, options.ArrayPool),
            options);
    }

    public static CsvRecordWriter<byte, CsvByteBufferWriter> Create(PipeWriter pipeWriter, CsvOptions<byte> options)
    {
        return new CsvRecordWriter<byte, CsvByteBufferWriter>(
            new CsvByteBufferWriter(pipeWriter),
            options);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowForInvalidTokensWritten(object formatter, int tokensWritten, int destinationLength)
    {
        throw new InvalidOperationException(
            $"Formatter ({formatter.GetType().ToTypeString()}) reported {tokensWritten} " +
            $"tokens written to a buffer of length {destinationLength}.");
    }
}
