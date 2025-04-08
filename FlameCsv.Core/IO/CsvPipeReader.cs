using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using FlameCsv.Extensions;
using FlameCsv.Utilities;
using JetBrains.Annotations;

namespace FlameCsv.IO;

/// <summary>
/// Static class that can be used to create <see cref="ICsvPipeReader{T}"/> instances.
/// </summary>
[PublicAPI]
[EditorBrowsable(EditorBrowsableState.Advanced)]
public static class CsvPipeReader
{
    /// <summary>
    /// Creates a new pipe reader over the CSV data.
    /// </summary>
    /// <remarks>
    /// The <see cref="StringBuilder"/> must not be modified while the reader is in use.
    /// </remarks>
    /// <param name="csv">String builder containing the CSV</param>
    public static ICsvPipeReader<char> Create(StringBuilder? csv)
        => Create(csv is null ? ReadOnlySequence<char>.Empty : StringBuilderSegment.Create(csv));

    /// <summary>
    /// Creates a new pipe reader over the CSV data.
    /// </summary>
    /// <param name="csv">CSV data</param>
    public static ICsvPipeReader<char> Create(string? csv) => Create(new ReadOnlySequence<char>(csv.AsMemory()));

    /// <inheritdoc cref="Create(string?)"/>
    public static ICsvPipeReader<char> Create(ReadOnlyMemory<char> csv) => Create(new ReadOnlySequence<char>(csv));

    /// <inheritdoc cref="Create(string?)"/>
    public static ICsvPipeReader<char> Create(in ReadOnlySequence<char> csv)
    {
        return new ConstantPipeReader<char>(in csv);
    }

    /// <inheritdoc cref="Create(string?)"/>
    public static ICsvPipeReader<byte> Create(ReadOnlyMemory<byte> csv) => Create(new ReadOnlySequence<byte>(csv));

    /// <inheritdoc cref="Create(string?)"/>
    public static ICsvPipeReader<byte> Create(in ReadOnlySequence<byte> csv)
    {
        return new ConstantPipeReader<byte>(in csv);
    }

    /// <inheritdoc cref="Create(string?)"/>
    [OverloadResolutionPriority(-1)]
    public static ICsvPipeReader<T> Create<T>(in ReadOnlySequence<T> csv) where T : unmanaged, IBinaryInteger<T>
    {
        return new ConstantPipeReader<T>(in csv);
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
    public static ICsvPipeReader<byte> Create(
        Stream stream,
        MemoryPool<byte>? memoryPool = null,
        CsvReaderOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Guard.CanRead(stream);

        if (!options.NoDirectBufferAccess &&
            stream is MemoryStream memoryStream &&
            memoryStream.TryGetBuffer(out ArraySegment<byte> buffer))
        {
            return new ConstantPipeReader<byte>(
                new ReadOnlySequence<byte>(buffer.AsMemory()),
                stream,
                options.LeaveOpen,
                static (stream, advanced) => ((MemoryStream)stream!).Seek(advanced, SeekOrigin.Current));
        }

        return new StreamPipeReader(stream, memoryPool ?? MemoryPool<byte>.Shared, in options);
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
    public static ICsvPipeReader<char> Create(
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
                ReadOnlyMemory<char> data = content.AsMemory(start: PositionRef(stringReader));
                return new ConstantPipeReader<char>(
                    new ReadOnlySequence<char>(data),
                    reader,
                    options.LeaveOpen,
                    static (reader, advanced) => PositionRef((StringReader)reader!) += (int)advanced);
            }

            // throw ODE
            _ = stringReader.Read();
        }

        return new TextPipeReader(reader, memoryPool ?? MemoryPool<char>.Shared, in options);

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
    public static ICsvPipeReader<char> Create(
        Stream stream,
        Encoding? encoding,
        MemoryPool<char>? memoryPool = null,
        CsvReaderOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Guard.CanRead(stream);

        return new TextPipeReader(
            new StreamReader(stream, encoding, bufferSize: options.BufferSize, leaveOpen: options.LeaveOpen),
            memoryPool ?? MemoryPool<char>.Shared,
            in options);
    }
}
