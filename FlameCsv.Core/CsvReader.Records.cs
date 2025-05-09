﻿using System.Text;
using FlameCsv.Extensions;
using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FlameCsv.Enumeration;
using FlameCsv.IO;

namespace FlameCsv;

public static partial class CsvReader
{
    /// <summary>
    /// Reads CSV records from the data.
    /// </summary>
    /// <remarks>
    /// If the options-instance is configured to have a header, the first returned record will be the one after that.
    /// <para/>
    /// The returned <see cref="CsvValueRecord{T}"/> instances are intended to be used only within a <c>foreach</c>-loop.
    /// The records are valid only until the next record is read, and accessing them at any point after that
    /// (including using LINQ methods such as <c>First()</c>) will result in a runtime error.
    /// Convert the record to <see cref="CsvRecord{T}"/> to make a copy of the data if you need to preserve it.
    /// </remarks>
    /// <param name="csv">CSV data</param>
    /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
    public static CsvRecordEnumerable<char> Enumerate(
        string? csv,
        CsvOptions<char>? options = null)
    {
        return new CsvRecordEnumerable<char>(csv.AsMemory(), options ?? CsvOptions<char>.Default);
    }

    /// <inheritdoc cref="Enumerate(string?,FlameCsv.CsvOptions{char}?)"/>
    public static CsvRecordEnumerable<char> Enumerate(
        ReadOnlyMemory<char> csv,
        CsvOptions<char>? options = null)
    {
        return new CsvRecordEnumerable<char>(csv, options ?? CsvOptions<char>.Default);
    }

    /// <inheritdoc cref="Enumerate(string?,FlameCsv.CsvOptions{char}?)"/>
    public static CsvRecordEnumerable<byte> Enumerate(
        ReadOnlyMemory<byte> csv,
        CsvOptions<byte>? options = null)
    {
        return new CsvRecordEnumerable<byte>(csv, options ?? CsvOptions<byte>.Default);
    }

    /// <inheritdoc cref="Enumerate(string?,FlameCsv.CsvOptions{char}?)"/>
    public static CsvRecordEnumerable<char> Enumerate(
        in ReadOnlySequence<char> csv,
        CsvOptions<char>? options = null)
    {
        return new CsvRecordEnumerable<char>(in csv, options ?? CsvOptions<char>.Default);
    }

    /// <inheritdoc cref="Enumerate(string?,FlameCsv.CsvOptions{char}?)"/>
    public static CsvRecordEnumerable<byte> Enumerate(
        in ReadOnlySequence<byte> csv,
        CsvOptions<byte>? options = null)
    {
        return new CsvRecordEnumerable<byte>(in csv, options ?? CsvOptions<byte>.Default);
    }

    /// <summary>
    /// Reads CSV records from the stream by creating a <see cref="StreamReader"/> using the provided options.
    /// </summary>
    /// <remarks><inheritdoc cref="Enumerate(string?,FlameCsv.CsvOptions{char}?)" path="/remarks"/></remarks>
    /// <param name="stream">Stream to read the records from</param>
    /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
    /// <param name="encoding">Encoding, defaulting to UTF8</param>
    /// <param name="ioOptions">Options to configure the inner reader</param>
    public static CsvRecordEnumerable<char> Enumerate(
        Stream stream,
        CsvOptions<char>? options = null,
        Encoding? encoding = null,
        CsvIOOptions ioOptions = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Guard.CanRead(stream);

        options ??= CsvOptions<char>.Default;
        return new CsvRecordEnumerable<char>(
            CsvBufferReader.Create(stream, encoding, options.Allocator, ioOptions),
            options);
    }

    /// <summary>
    /// Reads CSV records from the <see cref="TextReader"/>.
    /// </summary>
    /// <remarks><inheritdoc cref="Enumerate(string?,FlameCsv.CsvOptions{char}?)" path="/remarks"/></remarks>
    /// <param name="textReader">Text reader to read the records from</param>
    /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
    /// <param name="ioOptions">Options to configure the inner reader</param>
    public static CsvRecordEnumerable<char> Enumerate(
        TextReader textReader,
        CsvOptions<char>? options = null,
        CsvIOOptions ioOptions = default)
    {
        ArgumentNullException.ThrowIfNull(textReader);

        options ??= CsvOptions<char>.Default;
        return new CsvRecordEnumerable<char>(
            CsvBufferReader.Create(textReader, options.Allocator, ioOptions),
            options);
    }

    /// <summary>
    /// Reads CSV records from the <see cref="Stream"/>.
    /// </summary>
    /// <remarks><inheritdoc cref="Enumerate(string?,FlameCsv.CsvOptions{char}?)" path="/remarks"/></remarks>
    /// <param name="stream">Stream to read the records from</param>
    /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
    /// <param name="ioOptions">Options to configure the inner reader</param>
    [OverloadResolutionPriority(1)] // Prefer byte to char for ambiguous streams
    public static CsvRecordEnumerable<byte> Enumerate(
        Stream stream,
        CsvOptions<byte>? options = null,
        CsvIOOptions ioOptions = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        options ??= CsvOptions<byte>.Default;
        return new CsvRecordEnumerable<byte>(
            CsvBufferReader.Create(stream, options.Allocator, ioOptions),
            options);
    }

    /// <inheritdoc cref="Enumerate(string?,FlameCsv.CsvOptions{char}?)"/>
    [OverloadResolutionPriority(-1)] // prefer concrete types to generic
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvRecordEnumerable<T> Enumerate<T>(
        ReadOnlyMemory<T> csv,
        CsvOptions<T>? options = null)
        where T : unmanaged, IBinaryInteger<T>
    {
        return new CsvRecordEnumerable<T>(csv, options ?? CsvOptions<T>.Default);
    }

    /// <inheritdoc cref="Enumerate(string?,FlameCsv.CsvOptions{char}?)"/>
    [OverloadResolutionPriority(-1)] // prefer concrete types to generic
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CsvRecordEnumerable<T> Enumerate<T>(
        in ReadOnlySequence<T> csv,
        CsvOptions<T>? options = null)
        where T : unmanaged, IBinaryInteger<T>
    {
        return new CsvRecordEnumerable<T>(in csv, options ?? CsvOptions<T>.Default);
    }
}
