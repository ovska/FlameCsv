using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using FlameCsv.Reading;
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
    internal static ICsvPipeReader<byte> CreatePipeReader(
        Stream stream,
        MemoryPool<byte> memoryPool,
        bool leaveOpen)
    {
        if (stream is MemoryStream memoryStream && memoryStream.TryGetBuffer(out ArraySegment<byte> buffer))
        {
            return new ConstantPipeReader<byte>(
                buffer,
                stream,
                leaveOpen,
                static (stream, advanced) => ((MemoryStream)stream).Seek(advanced, SeekOrigin.Current));
        }

        Guard.CanRead(stream);
        return new PipeReaderWrapper(
            PipeReader.Create(stream, new StreamPipeReaderOptions(pool: memoryPool, leaveOpen: leaveOpen)));
    }

    /// <summary>
    /// Creates a PipeReader from a TextReader.
    /// </summary>
    /// <returns></returns>
    internal static ICsvPipeReader<char> CreatePipeReader(
        TextReader reader,
        MemoryPool<char> memoryPool,
        int bufferSize)
    {
        // zero-copy reads if the reader is a StringReader and hasn't been read yet
        if (reader.GetType() == typeof(StringReader))
        {
            var stringReader = (StringReader)reader;
            string? content = GetString(stringReader);

            // not yet disposed
            if (content is not null)
            {
                ReadOnlyMemory<char> data = content.AsMemory(start: GetPosition(stringReader));
                return new ConstantPipeReader<char>(
                    data,
                    reader,
                    leaveOpen: false,
                    static (reader, advanced) => GetPosition((StringReader)reader) += (int)advanced);
            }
        }

        return new TextPipeReader(reader, bufferSize, memoryPool);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_s")]
        static extern ref string? GetString(StringReader stringReader);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_pos")]
        static extern ref int GetPosition(StringReader stringReader);
    }
}
