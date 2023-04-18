using System.Diagnostics.CodeAnalysis;
using FlameCsv.Exceptions;

namespace FlameCsv;

/// <summary>
/// A lazily-enumerated wrapper around a single CSV record.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public interface ICsvRecord<T> : IEnumerable<ReadOnlyMemory<T>> where T : unmanaged, IEquatable<T>
{
    /// <inheritdoc cref="GetField(int)"/>
    ReadOnlyMemory<T> this[int index] { get; }

    /// <summary>
    /// 0-based token position of this record's beginning from the start of the CSV.
    /// </summary>
    /// <remarks>First record's position is always 0.</remarks>
    long Position { get; }

    /// <summary>
    /// 1-based line number in the CSV.
    /// </summary>
    /// <remarks>First record's line number is always 1.</remarks>
    int Line { get; }

    /// <summary>
    /// CSV dialect used to parse the records.
    /// </summary>
    CsvDialect<T> Dialect { get; }

    /// <summary>
    /// The complete unescaped data on the line without trailing newline tokens.
    /// </summary>
    /// <remarks>
    /// Reference to the data must not be held onto after the next record has been read.
    /// If the data is needed later, copy the data into a separate array.
    /// </remarks>
    ReadOnlyMemory<T> Data { get; }

    /// <summary>
    /// Returns the data at column at <paramref name="index"/>.
    /// </summary>
    /// <remarks>
    /// Reference to the data must not be held onto after the next record has been read.
    /// If the data is needed later, copy the data into a separate array.
    /// </remarks>
    /// <param name="index">0-based column index, e.g. 0 for the first column</param>
    /// <returns>Column value, unescaped and stripped of quotes when applicable</returns>
    /// <exception cref="ArgumentOutOfRangeException"/>
    ReadOnlyMemory<T> GetField(int index);

    /// <summary>
    /// Returns the number of fields in the current record.
    /// </summary>
    /// <remarks>
    /// Reads the CSV record its' entirety if it already hasn't been tokenized.
    /// </remarks>
    int GetFieldCount();

    /// <inheritdoc cref="TryGetValue{TValue}(int, out TValue, out CsvGetValueReason)"/>
    bool TryGetValue<TValue>(int index, [MaybeNullWhen(false)] out TValue value);

    /// <summary>
    /// Attempts to parse a <typeparamref name="TValue"/> from field at <paramref name="index"/>.
    /// </summary>
    /// <typeparam name="TValue">Value parsed</typeparam>
    /// <param name="index">0-based field index</param>
    /// <param name="value">Parsed value, if successful</param>
    /// <param name="reason">Reason for the failure</param>
    /// <returns><see langword="true"/> if the value was successfully parsed</returns>
    bool TryGetValue<TValue>(int index, [MaybeNullWhen(false)] out TValue value, out CsvGetValueReason reason);

    /// <summary>
    /// Parses a value of type <typeparamref name="TValue"/> from field at <paramref name="index"/>.
    /// </summary>
    /// <typeparam name="TValue">Value parsed</typeparam>
    /// <param name="index">0-based field index</param>
    /// <returns>Parsed value</returns>
    /// <exception cref="ArgumentOutOfRangeException"/>
    /// <exception cref="CsvParserMissingException"/>
    /// <exception cref="CsvParseException"/>
    TValue GetField<TValue>(int index);
}

