using System.Buffers;

namespace FlameCsv;

public static partial class CsvReader
{
    /// <inheritdoc cref="Enumerate{T}(ReadOnlySequence{T},CsvReaderOptions{T})"/>
    public static CsvEnumerator<char> Enumerate(
        string csv,
        CsvReaderOptions<char> options)
    {
        ArgumentNullException.ThrowIfNull(csv);
        ArgumentNullException.ThrowIfNull(options);
        return new CsvEnumerator<char>(new(csv.AsMemory()), options, null);
    }

    /// <inheritdoc cref="Enumerate{T}(ReadOnlySequence{T},CsvReaderOptions{T})"/>
    public static CsvEnumerator<T> Enumerate<T>(
        ReadOnlyMemory<T> csv,
        CsvReaderOptions<T> options)
        where T : unmanaged, IEquatable<T>
    {
        ArgumentNullException.ThrowIfNull(options);

        return new CsvEnumerator<T>(new ReadOnlySequence<T>(csv), options, null);
    }

    /// <summary>
    /// Returns an enumerator that can be used to read CSV records in a forward-only fashion.
    /// </summary>
    /// <remarks>
    /// The enumerator should either be used in a <see langword="foreach"/>-block or disposed explicitly.
    /// </remarks>
    /// <param name="csv">Data to read the records from</param>
    /// <param name="options">Options instance containing tokens and parsers</param>
    /// <returns>A CSV-enumerator structure</returns>
    public static CsvEnumerator<T> Enumerate<T>(
        ReadOnlySequence<T> csv,
        CsvReaderOptions<T> options)
        where T : unmanaged, IEquatable<T>
    {
        ArgumentNullException.ThrowIfNull(options);

        return new CsvEnumerator<T>(csv, options, null);
    }
}
