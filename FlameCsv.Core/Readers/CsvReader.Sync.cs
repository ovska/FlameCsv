using System.Buffers;
using FlameCsv.Binding.Providers;

namespace FlameCsv.Readers;

public static partial class CsvReader
{
    // to avoid using two generics and AsMemory() for common operations
    /// <inheritdoc cref="Read{T,TValue}(CsvReaderOptions{T},ReadOnlyMemory{T})"/>
    public static IEnumerable<TValue> Read<TValue>(
        CsvReaderOptions<char> options,
        string csv)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(csv);
        return Read<char, TValue>(options, new ReadOnlySequence<char>(csv.AsMemory()));
    }

    // to avoid using two generics for common operations
    /// <inheritdoc cref="Read{T,TValue}(CsvReaderOptions{T},ReadOnlyMemory{T})"/>
    public static IEnumerable<TValue> Read<TValue>(
        CsvReaderOptions<char> options,
        ReadOnlyMemory<char> csv)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Read<char, TValue>(options, new ReadOnlySequence<char>(csv));
    }

    // to avoid using two generics for common operations
    /// <inheritdoc cref="Read{T,TValue}(CsvReaderOptions{T},ReadOnlyMemory{T})"/>
    public static IEnumerable<TValue> Read<TValue>(
        CsvReaderOptions<byte> readerOptions,
        ReadOnlyMemory<byte> csv)
    {
        ArgumentNullException.ThrowIfNull(readerOptions);
        return Read<byte, TValue>(readerOptions, new ReadOnlySequence<byte>(csv));
    }

    /// <summary>
    /// Synchronously reads <typeparamref name="TValue"/> from the data.
    /// </summary>
    /// <param name="csv">Data to read the records from</param>
    /// <param name="options">Options instance containing tokens and parsers</param>
    /// <returns><see cref="IEnumerable{T}"/> that reads records line-by-line from the data.</returns>
    public static IEnumerable<TValue> Read<T, TValue>(
        CsvReaderOptions<T> options,
        ReadOnlyMemory<T> csv)
        where T : unmanaged, IEquatable<T>
    {
        ArgumentNullException.ThrowIfNull(options);
        return Read<T, TValue>(options, new ReadOnlySequence<T>(csv));
    }

    /// <inheritdoc cref="Read{T,TValue}(CsvReaderOptions{T},ReadOnlyMemory{T})"/>
    public static IEnumerable<TValue> Read<T, TValue>(
        CsvReaderOptions<T> readerOptions,
        ReadOnlySequence<T> csv)
        where T : unmanaged, IEquatable<T>
    {
        if (readerOptions.BindingProvider is ICsvHeaderBindingProvider<T>)
        {
            var processor = new CsvHeaderProcessor<T, TValue>(readerOptions);
            return ReadInternal<T, TValue, CsvHeaderProcessor<T, TValue>>(csv, processor);
        }
        else
        {
            var processor = new CsvProcessor<T, TValue>(readerOptions);
            return ReadInternal<T, TValue, CsvProcessor<T, TValue>>(csv, processor);
        }
    }

    private static IEnumerable<TValue> ReadInternal<T, TValue, TProcessor>(
        ReadOnlySequence<T> buffer,
        TProcessor processor)
        where T : unmanaged, IEquatable<T>
        where TProcessor : struct, ICsvProcessor<T, TValue>
    {
        TValue value;

        while (processor.TryContinueRead(ref buffer, out value))
        {
            yield return value;
        }

        if (!buffer.IsEmpty && processor.TryReadRemaining(in buffer, out value))
        {
            yield return value;
        }
    }
}
