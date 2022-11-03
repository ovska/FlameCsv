using System.Buffers;
using FlameCsv.Binding.Providers;

namespace FlameCsv.Readers;

public static partial class CsvReader
{
    // to avoid using two generics and AsMemory() for common operations
    /// <inheritdoc cref="Read{T,TValue}(CsvReaderOptions{T},ReadOnlyMemory{T})"/>
    public static IEnumerable<TValue> Read<TValue>(
        string csv,
        CsvReaderOptions<char> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(csv);
        return Read<char, TValue>(new ReadOnlySequence<char>(csv.AsMemory()), options);
    }

    // to avoid using two generics for common operations
    /// <inheritdoc cref="Read{T,TValue}(CsvReaderOptions{T},ReadOnlyMemory{T})"/>
    public static IEnumerable<TValue> Read<TValue>(
        ReadOnlyMemory<char> csv,
        CsvReaderOptions<char> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Read<char, TValue>(new ReadOnlySequence<char>(csv), options);
    }

    // to avoid using two generics for common operations
    /// <inheritdoc cref="Read{T,TValue}(CsvReaderOptions{T},ReadOnlyMemory{T})"/>
    public static IEnumerable<TValue> Read<TValue>(
        ReadOnlyMemory<byte> csv,
        CsvReaderOptions<byte> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Read<byte, TValue>(new ReadOnlySequence<byte>(csv), options);
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
        return Read<T, TValue>(new ReadOnlySequence<T>(csv), options);
    }

    /// <inheritdoc cref="Read{T,TValue}(CsvReaderOptions{T},ReadOnlyMemory{T})"/>
    public static IEnumerable<TValue> Read<T, TValue>(
        ReadOnlySequence<T> csv,
        CsvReaderOptions<T> options)
        where T : unmanaged, IEquatable<T>
    {
        if (options.BindingProvider is ICsvHeaderBindingProvider<T>)
        {
            var processor = new CsvHeaderProcessor<T, TValue>(options);
            return ReadInternal<T, TValue, CsvHeaderProcessor<T, TValue>>(csv, processor);
        }
        else
        {
            var processor = new CsvProcessor<T, TValue>(options);
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
