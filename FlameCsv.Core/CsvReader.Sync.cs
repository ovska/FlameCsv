using System.Buffers;
using CommunityToolkit.HighPerformance;

namespace FlameCsv;

public static partial class CsvReader
{
    // to avoid user having to use two generics for common operations
    /// <inheritdoc cref="Read{T,TValue}(CsvReaderOptions{T},ReadOnlyMemory{T})"/>
    public static CsvRecordEnumerable<char, TValue> Read<TValue>(
        string? csv,
        CsvReaderOptions<char> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new CsvRecordEnumerable<char, TValue>(options, new ReadOnlySequence<char>(csv.AsMemory()));
    }

    // to avoid user having to use two generics for common operations
    /// <inheritdoc cref="Read{T,TValue}(CsvReaderOptions{T},ReadOnlyMemory{T})"/>
    public static CsvRecordEnumerable<char, TValue> Read<TValue>(
        ReadOnlyMemory<char> csv,
        CsvReaderOptions<char> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new CsvRecordEnumerable<char, TValue>(options, new ReadOnlySequence<char>(csv));
    }

    // to avoid user having to use two generics for common operations
    /// <inheritdoc cref="Read{T,TValue}(CsvReaderOptions{T},ReadOnlyMemory{T})"/>
    public static CsvRecordEnumerable<byte, TValue> Read<TValue>(
        ReadOnlyMemory<byte> csv,
        CsvReaderOptions<byte> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new CsvRecordEnumerable<byte, TValue>(options, new ReadOnlySequence<byte>(csv));
    }

    /// <summary>
    /// Synchronously reads <typeparamref name="TValue"/> from the data.
    /// </summary>
    /// <param name="csv">Data to read the records from</param>
    /// <param name="options">Options instance containing tokens and parsers</param>
    /// <returns><see cref="IEnumerable{T}"/> that reads records line-by-line from the data.</returns>
    public static CsvRecordEnumerable<T, TValue> Read<T, TValue>(
        ReadOnlyMemory<T> csv,
        CsvReaderOptions<T> options)
        where T : unmanaged, IEquatable<T>
    {
        ArgumentNullException.ThrowIfNull(options);
        return new CsvRecordEnumerable<T, TValue>(options, new ReadOnlySequence<T>(csv));
    }

    /// <inheritdoc cref="Read{T,TValue}(CsvReaderOptions{T},ReadOnlyMemory{T})"/>
    public static CsvRecordEnumerable<T, TValue> Read<T, TValue>(
        ReadOnlySequence<T> csv,
        CsvReaderOptions<T> options)
        where T : unmanaged, IEquatable<T>
    {
        ArgumentNullException.ThrowIfNull(options);
        return new CsvRecordEnumerable<T, TValue>(options, csv);
    }
}

