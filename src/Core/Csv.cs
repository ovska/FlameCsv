using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;
using FlameCsv.IO;
using FlameCsv.Utilities;

namespace FlameCsv;

/// <summary>
/// Provides static methods to read and write CSV.<br/>
/// To read CSV, see <c>From</c> and <c>FromFile</c> methods.<br/>
/// To write CSV, see <c>To</c> and <c>ToFile</c> methods.
/// </summary>
public static partial class Csv
{
    #region Reading

    /// <summary>
    /// Creates a reader builder from the given CSV data.
    /// </summary>
    /// <param name="csv">CSV data</param>
    /// <returns>Builder to create a CSV reading pipeline from</returns>
    public static IReadBuilderBase<char> From(string? csv) => new ReadMemoryBuilder<char>(csv.AsMemory());

    /// <inheritdoc cref="From(string?)"/>
    public static IReadBuilderBase<char> From(ReadOnlyMemory<char> csv) => new ReadMemoryBuilder<char>(csv);

    /// <inheritdoc cref="From(string?)"/>
    public static IReadBuilderBase<byte> From(ReadOnlyMemory<byte> csv) => new ReadMemoryBuilder<byte>(csv);

    /// <summary>
    /// Creates a reader builder from the given CSV data.
    /// </summary>
    /// <param name="csv">CSV data</param>
    /// <param name="ioOptions">Options to configure the buffer size and other IO related options</param>
    /// <returns>Builder to create a CSV reading pipeline from</returns>
    public static IReadBuilder<char> From(in ReadOnlySequence<char> csv, CsvIOOptions ioOptions = default) =>
        new ReadSequenceBuilder<char>(in csv, in ioOptions);

    /// <inheritdoc cref="From(in ReadOnlySequence{char}, CsvIOOptions)"/>
    public static IReadBuilder<byte> From(in ReadOnlySequence<byte> csv, CsvIOOptions ioOptions = default) =>
        new ReadSequenceBuilder<byte>(in csv, in ioOptions);

    [OverloadResolutionPriority(-1)]
    internal static IReadBuilder<T> From<T>(in ReadOnlySequence<T> csv, CsvIOOptions ioOptions = default)
        where T : unmanaged, IBinaryInteger<T> => new ReadSequenceBuilder<T>(in csv, in ioOptions);

    /// <inheritdoc cref="From(in ReadOnlySequence{char}, CsvIOOptions)"/>
    /// <remarks>
    /// The <see cref="StringBuilder"/> must not be modified while the reader is in use.
    /// </remarks>
    public static IReadBuilder<char> From(StringBuilder csv, CsvIOOptions ioOptions = default) =>
        new ReadSequenceBuilder<char>(StringBuilderSegment.Create(csv), in ioOptions);

    /// <summary>
    /// Creates a reader builder from the given <see cref="TextReader"/>.
    /// </summary>
    /// <param name="reader">Reader to read the CSV data from</param>
    /// <param name="ioOptions">Options to configure the buffer size and other IO related options</param>
    /// <returns>Builder to create a CSV reading pipeline from</returns>
    public static IReadBuilder<char> From(TextReader reader, CsvIOOptions ioOptions = default) =>
        new ReadTextBuilder(reader, in ioOptions);

    /// <summary>
    /// Creates a reader builder from the given <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">Stream to read the CSV data from</param>
    /// <param name="ioOptions">Options to configure the buffer size and other IO related options</param>
    /// <returns>Builder to create a CSV reading pipeline from</returns>
    /// <remarks>
    /// This method reads the bytes directly as UTF8. To work with <c>char</c>,
    /// see <see cref="From(Stream, Encoding?, CsvIOOptions)"/> or <see cref="IReadStreamBuilder.WithEncoding(Encoding)"/>.
    /// </remarks>
    public static IReadStreamBuilder From(Stream stream, CsvIOOptions ioOptions = default) =>
        new ReadStreamBuilder(stream, in ioOptions);

    /// <summary>
    /// Creates a reader builder from the given file.
    /// </summary>
    /// <param name="path">Path to the file to read the CSV data from</param>
    /// <param name="ioOptions">Options to configure the buffer size and other IO related options</param>
    /// <returns>Builder to create a CSV reading pipeline from</returns>
    /// <remarks>
    /// This method reads the bytes directly as UTF8. To work with <c>char</c>,
    /// see <see cref="FromFile(string, Encoding?, CsvIOOptions)"/> or <see cref="IReadStreamBuilder.WithEncoding(Encoding)"/>.
    /// </remarks>
    public static IReadStreamBuilder FromFile(string path, CsvIOOptions ioOptions = default) =>
        new FileReaderBuilder(path, in ioOptions);

    /// <summary>
    /// Creates a reader builder from the given <see cref="Stream"/> in the specified encoding.
    /// </summary>
    /// <param name="stream">Stream to read the CSV data from</param>
    /// <param name="encoding">Encoding of the stream, defaults to UTF8</param>
    /// <param name="ioOptions">Options to configure the buffer size and other IO related options</param>
    /// <returns>Builder to create a CSV reading pipeline from</returns>
    public static IReadBuilder<char> From(Stream stream, Encoding? encoding, CsvIOOptions ioOptions = default) =>
        new ReadTextBuilder(stream, encoding, in ioOptions);

    /// <summary>
    /// Creates a reader builder from the given file using the specified encoding.
    /// </summary>
    /// <param name="path">Path to the file to read the CSV data from</param>
    /// <param name="encoding">Encoding of the stream, defaults to UTF8</param>
    /// <param name="ioOptions">Options to configure the buffer size and other IO related options</param>
    /// <returns>Builder to create a CSV reading pipeline from</returns>
    public static IReadBuilder<char> FromFile(string path, Encoding? encoding, CsvIOOptions ioOptions = default) =>
        new FileReaderBuilder(path, in ioOptions, encoding);

    #endregion Reading

    #region Writing

    /// <summary>
    /// Creates a writer builder from the given CSV data.
    /// </summary>
    /// <param name="stringBuilder">StringBuilder to write the CSV data to</param>
    /// <param name="ioOptions">Options to configure the buffer size and other IO related options</param>
    /// <returns>Builder to create a CSV writing pipeline from</returns>
    public static IWriteBuilder<char> To(StringBuilder stringBuilder, CsvIOOptions ioOptions = default)
    {
        ArgumentNullException.ThrowIfNull(stringBuilder);
        return new WriteTextBuilder(new StringWriter(stringBuilder), in ioOptions);
    }

    /// <summary>
    /// Creates a writer builder from the given CSV data.
    /// </summary>
    /// <param name="writer">Writer to write the CSV data to</param>
    /// <param name="ioOptions">Options to configure the buffer size and other IO related options</param>
    /// <returns>Builder to create a CSV writing pipeline from</returns>
    public static IWriteBuilder<char> To(TextWriter writer, CsvIOOptions ioOptions = default) =>
        new WriteTextBuilder(writer, in ioOptions);

    /// <summary>
    /// Creates a writer builder from the given CSV data.
    /// </summary>
    /// <param name="stream">Stream to write the CSV data to</param>
    /// <param name="ioOptions">Options to configure the buffer size and other IO related options</param>
    /// <returns>Builder to create a CSV writing pipeline from</returns>
    /// <remarks>
    /// This method writes bytes directly as UTF8. To work with <c>char</c>,
    /// see <see cref="ToFile(string, Encoding?, CsvIOOptions)"/> or <see cref="IWriteStreamBuilder.WithEncoding(Encoding)"/>.
    /// </remarks>
    public static IWriteStreamBuilder To(Stream stream, CsvIOOptions ioOptions = default) =>
        new WriteStreamBuilder(stream, in ioOptions);

    /// <summary>
    /// Creates a writer builder from the given CSV data.
    /// </summary>
    /// <param name="stream">Stream to write the CSV data to</param>
    /// <param name="encoding">Encoding of the stream, defaults to UTF8</param>
    /// <param name="ioOptions">Options to configure the buffer size and other IO related options</param>
    /// <returns>Builder to create a CSV writing pipeline from</returns>
    public static IWriteBuilder<char> To(Stream stream, Encoding? encoding, CsvIOOptions ioOptions = default) =>
        new WriteTextBuilder(stream, encoding, in ioOptions);

    /// <summary>
    /// Creates a writer builder from the given CSV data.
    /// </summary>
    /// <param name="pipeWriter">PipeWriter to write the CSV data to</param>
    /// <param name="bufferPool">Buffer pool to get temporary buffers from, defaults to <see cref="MemoryPool{T}.Shared"/></param>
    /// <returns>Builder to create a CSV writing pipeline from</returns>
    public static IWriteBuilderBase<byte> To(PipeWriter pipeWriter, IBufferPool? bufferPool = null) =>
        new WritePipeBuilder(pipeWriter, bufferPool);

    /// <summary>
    /// Creates a writer builder to write CSV data to a file.
    /// </summary>
    /// <param name="path">Path to the file to write the CSV data to</param>
    /// <param name="ioOptions">Options to configure the buffer size and other IO related options</param>
    /// <returns>Builder to create a CSV writing pipeline from</returns>
    /// <remarks>
    /// This method writes bytes directly as UTF8. To work with <c>char</c>,
    /// see <see cref="ToFile(string, Encoding?, CsvIOOptions)"/> or <see cref="IWriteStreamBuilder.WithEncoding(Encoding)"/>.
    /// </remarks>
    public static IWriteStreamBuilder ToFile(string path, CsvIOOptions ioOptions = default) =>
        new WriteFileBuilder(path, null, in ioOptions);

    /// <summary>
    /// Creates a writer builder to write CSV data to a file using the specified encoding.
    /// </summary>
    /// <param name="path">Path to the file to write the CSV data to</param>
    /// <param name="encoding">Encoding of the file, defaults to UTF8</param>
    /// <param name="ioOptions">Options to configure the buffer size and other IO related options</param>
    /// <returns>Builder to create a CSV writing pipeline from</returns>
    public static IWriteBuilder<char> ToFile(string path, Encoding? encoding, CsvIOOptions ioOptions = default) =>
        new WriteFileBuilder(path, encoding, in ioOptions);

    #endregion Writing
}
