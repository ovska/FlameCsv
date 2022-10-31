using System.Buffers;
using System.Runtime.CompilerServices;
using FlameCsv.Binding.Providers;

namespace FlameCsv.Readers;

public static partial class CsvReader
{
    // to avoid using two generics and AsMemory() for common operations
    /// <inheritdoc cref="Read{T,TValue}(CsvConfiguration{T},ReadOnlyMemory{T})"/>
    public static IEnumerable<TValue> Read<TValue>(
        CsvConfiguration<char> configuration,
        string csv)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(csv);
        return Read<char, TValue>(configuration, new ReadOnlySequence<char>(csv.AsMemory()));
    }

    // to avoid using two generics for common operations
    /// <inheritdoc cref="Read{T,TValue}(CsvConfiguration{T},ReadOnlyMemory{T})"/>
    public static IEnumerable<TValue> Read<TValue>(
        CsvConfiguration<char> configuration,
        ReadOnlyMemory<char> csv)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return Read<char, TValue>(configuration, new ReadOnlySequence<char>(csv));
    }

    // to avoid using two generics for common operations
    /// <inheritdoc cref="Read{T,TValue}(CsvConfiguration{T},ReadOnlyMemory{T})"/>
    public static IEnumerable<TValue> Read<TValue>(
        CsvConfiguration<byte> configuration,
        ReadOnlyMemory<byte> csv)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return Read<byte, TValue>(configuration, new ReadOnlySequence<byte>(csv));
    }

    /// <summary>
    /// Synchronously reads <typeparamref name="TValue"/> from the data.
    /// </summary>
    /// <param name="configuration">Configuration instance to use for binding and parsing</param>
    /// <param name="csv">Data to read the records from</param>
    /// <returns><see cref="IEnumerable{T}"/> that reads records line-by-line from the data.</returns>
    public static IEnumerable<TValue> Read<T, TValue>(
        CsvConfiguration<T> configuration,
        ReadOnlyMemory<T> csv)
        where T : unmanaged, IEquatable<T>
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return Read<T, TValue>(configuration, new ReadOnlySequence<T>(csv));
    }

    /// <inheritdoc cref="Read{T,TValue}(CsvConfiguration{T},ReadOnlyMemory{T})"/>
    public static IEnumerable<TValue> Read<T, TValue>(
        CsvConfiguration<T> configuration,
        ReadOnlySequence<T> csv)
        where T : unmanaged, IEquatable<T>
    {
        if (configuration.BindingProvider is ICsvHeaderBindingProvider<T>)
        {
            var processor = new CsvHeaderProcessor<T, TValue>(configuration);
            return ReadInternal<T, TValue, CsvHeaderProcessor<T, TValue>>(csv, processor);
        }
        else
        {
            var processor = new CsvProcessor<T, TValue>(configuration);
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
