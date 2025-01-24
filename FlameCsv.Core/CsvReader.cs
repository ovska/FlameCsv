using System.Buffers;
using System.IO.Pipelines;
using FlameCsv.Extensions;
using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// Provides static methods for reading CSV data.
/// </summary>
[PublicAPI]
public static partial class CsvReader
{
    /// <summary>
    /// Default buffer size used when creating a <see cref="PipeReader"/>
    /// or a <see cref="TextReader"/>.
    /// </summary>
    public const int DefaultBufferSize = 4096;

    /// <summary>
    /// Creates a PipeReader from a Stream.
    /// </summary>
    private static PipeReader CreatePipeReader(
        Stream stream,
        MemoryPool<byte> memoryPool,
        bool leaveOpen)
    {
        Guard.CanRead(stream);
        return PipeReader.Create(stream, new StreamPipeReaderOptions(pool: memoryPool, leaveOpen: leaveOpen));
    }
}
