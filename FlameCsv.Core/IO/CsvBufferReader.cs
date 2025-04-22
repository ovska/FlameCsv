using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using FlameCsv.Extensions;
using FlameCsv.Utilities;
using JetBrains.Annotations;

namespace FlameCsv.IO;

/// <summary>
/// Static class that can be used to create <see cref="ICsvBufferReader{T}"/> instances.
/// </summary>
[PublicAPI]
[EditorBrowsable(EditorBrowsableState.Advanced)]
public static class CsvBufferReader
{
    /// <summary>
    /// Creates a new reader over the CSV data.
    /// </summary>
    /// <remarks>
    /// The <see cref="StringBuilder"/> must not be modified while the reader is in use.
    /// </remarks>
    /// <param name="csv">String builder containing the CSV</param>
    public static ICsvBufferReader<char> Create(StringBuilder? csv)
        => Create(csv is null ? ReadOnlySequence<char>.Empty : StringBuilderSegment.Create(csv));

    /// <summary>
    /// Creates a new pipe reader over the CSV data.
    /// </summary>
    /// <param name="csv">CSV data</param>
    public static ICsvBufferReader<char> Create(string? csv)
    {
        return Create(csv.AsMemory());
    }

    /// <inheritdoc cref="Create(string?)"/>
    public static ICsvBufferReader<char> Create(ReadOnlyMemory<char> csv)
    {
        return new ConstantBufferReader<char>(csv);
    }

    /// <inheritdoc cref="Create(string?)"/>
    [OverloadResolutionPriority(-1)]
    public static ICsvBufferReader<T> Create<T>(ReadOnlyMemory<T> csv) where T : unmanaged
    {
        return new ConstantBufferReader<T>(csv);
    }

    /// <inheritdoc cref="Create(string?)"/>
    public static ICsvBufferReader<char> Create(
        in ReadOnlySequence<char> csv,
        MemoryPool<char>? memoryPool = null,
        CsvReaderOptions options = default)
    {
        if (csv.IsSingleSegment) return Create(csv.First);
        return new ConstantSequenceReader<char>(in csv, memoryPool ?? MemoryPool<char>.Shared, in options);
    }

    /// <inheritdoc cref="Create(string?)"/>
    public static ICsvBufferReader<byte> Create(ReadOnlyMemory<byte> csv)
    {
        return new ConstantBufferReader<byte>(csv);
    }

    /// <inheritdoc cref="Create(string?)"/>
    public static ICsvBufferReader<byte> Create(
        in ReadOnlySequence<byte> csv,
        MemoryPool<byte>? memoryPool = null,
        CsvReaderOptions options = default)
    {
        if (csv.IsSingleSegment) return Create(csv.First);
        return new ConstantSequenceReader<byte>(in csv, memoryPool ?? MemoryPool<byte>.Shared, in options);
    }

    /// <inheritdoc cref="Create(string?)"/>
    [OverloadResolutionPriority(-1)]
    public static ICsvBufferReader<T> Create<T>(
        in ReadOnlySequence<T> csv,
        MemoryPool<T>? memoryPool = null,
        CsvReaderOptions options = default)
        where T : unmanaged
    {
        if (csv.IsSingleSegment) return new ConstantBufferReader<T>(csv.First);
        return new ConstantSequenceReader<T>(in csv, memoryPool ?? MemoryPool<T>.Shared, in options);
    }

    /// <summary>
    /// Creates a new CSV reader instance from a <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">The stream</param>
    /// <param name="memoryPool">Memory pool used; defaults to <see cref="MemoryPool{T}.Shared"/>.</param>
    /// <param name="options">Options to configure the reader</param>
    /// <returns></returns>
    /// <remarks>
    /// If the stream is a <see cref="MemoryStream"/>, the buffer is accessed directly for zero-copy reads if possible.
    /// </remarks>
    public static ICsvBufferReader<byte> Create(
        Stream stream,
        MemoryPool<byte>? memoryPool = null,
        CsvReaderOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Guard.CanRead(stream);

        if (!options.NoDirectBufferAccess &&
            stream is MemoryStream { Position: var position and < int.MaxValue } memoryStream &&
            memoryStream.TryGetBuffer(out ArraySegment<byte> buffer))
        {
            return new ConstantBufferReader<byte>(
                buffer.AsMemory(start: (int)position),
                options.LeaveOpen,
                stream,
                static (stream, advanced) => ((MemoryStream)stream!).Seek(advanced, SeekOrigin.Current));
        }

        return new StreamBufferReader(stream, memoryPool ?? MemoryPool<byte>.Shared, in options);
    }

    /// <summary>
    /// Creates a new CSV reader instance from a <see cref="TextReader"/>.
    /// </summary>
    /// <param name="reader">The text reader</param>
    /// <param name="memoryPool">Memory pool used; defaults to <see cref="MemoryPool{T}.Shared"/>.</param>
    /// <param name="options">Options to configure the reader</param>
    /// <returns></returns>
    /// <remarks>
    /// If the stream is a <see cref="StringReader"/>, the internal string is accessed directly for zero-copy reads.
    /// </remarks>
    public static ICsvBufferReader<char> Create(
        TextReader reader,
        MemoryPool<char>? memoryPool = null,
        CsvReaderOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(reader);

        // zero-copy reads if the reader is a StringReader and hasn't been fully read yet
        if (!options.NoDirectBufferAccess && reader.GetType() == typeof(StringReader))
        {
            var stringReader = (StringReader)reader;
            string? content = GetString(stringReader);

            // "_s" is null if disposed
            if (content is not null)
            {
                return new ConstantBufferReader<char>(
                    content.AsMemory(start: PositionRef(stringReader)),
                    options.LeaveOpen,
                    reader,
                    static (reader, advanced) => PositionRef((StringReader)reader!) += advanced);
            }

            // throw ODE
            _ = stringReader.Read();
        }

        return new TextBufferReader(reader, memoryPool ?? MemoryPool<char>.Shared, in options);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_s")]
        static extern ref string? GetString(StringReader stringReader);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_pos")]
        static extern ref int PositionRef(StringReader stringReader);
    }

    /// <summary>
    /// Creates a new CSV reader instance from a <see cref="Stream"/> and an <see cref="Encoding"/>.
    /// </summary>
    /// <param name="stream">The stream</param>
    /// <param name="encoding">Encoding used to read the bytes</param>
    /// <param name="memoryPool">Memory pool used; defaults to <see cref="MemoryPool{T}.Shared"/>.</param>
    /// <param name="options">Options to configure the reader</param>
    /// <returns></returns>
    public static ICsvBufferReader<char> Create(
        Stream stream,
        Encoding? encoding,
        MemoryPool<char>? memoryPool = null,
        CsvReaderOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Guard.CanRead(stream);

        return new TextBufferReader(
            new StreamReader(stream, encoding, bufferSize: options.BufferSize, leaveOpen: options.LeaveOpen),
            memoryPool ?? MemoryPool<char>.Shared,
            in options);
    }
}
