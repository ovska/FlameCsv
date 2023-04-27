using System.Diagnostics.CodeAnalysis;
using FlameCsv.Exceptions;

namespace FlameCsv;

/// <summary>
/// An instance representing a single CSV record.
/// </summary>
/// <remarks>
/// The data in the record is read lazily. Subsequent operations will use cached data if possible.<br/>
/// References to the record or its fields must not be held onto after the next record has been read.
/// Parse the data or make a copy of the data if you need to hold onto it.
/// </remarks>
/// <typeparam name="T">Token type</typeparam>
public interface ICsvRecord<T> where T : unmanaged, IEquatable<T>
{
    /// <inheritdoc cref="GetField(int)"/>
    ReadOnlyMemory<T> this[int index] { get; }

    /// <inheritdoc cref="GetField(string)"/>
    ReadOnlyMemory<T> this[string name] { get; }

    bool HasHeader { get; }

    /// <summary>
    /// 0-based token position of this record's beginning from the start of the CSV.
    /// </summary>
    /// <remarks>First record's position is always 0.</remarks>
    long Position { get; }

    /// <summary>
    /// 1-based logical line number in the CSV. The header record is counted as a line.
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
    ReadOnlyMemory<T> RawRecord { get; }

    /// <summary>
    /// Returns the value of the field at the specified index.
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
    /// Returns the value of the field with the specified name. Requires for the CSV to have a header record.
    /// </summary>
    /// <remarks>
    /// The CSV must have a header record.<br/>
    /// Reference to the data must not be held onto after the next record has been read.
    /// If the data is needed later, copy the data into a separate array.
    /// </remarks>
    /// <param name="name">Header name to get the field for</param>
    /// <returns>Column value, unescaped and stripped of quotes when applicable</returns>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="InvalidOperationException"/>
    ReadOnlyMemory<T> GetField(string name);

    /// <summary>
    /// Returns the number of fields in the current record.
    /// </summary>
    /// <remarks>
    /// Reads the CSV record its' entirety if it already hasn't been tokenized.
    /// </remarks>
    int GetFieldCount();

    /// <summary>
    /// Attempts to parse a <typeparamref name="TValue"/> from field at <paramref name="index"/>.
    /// </summary>
    /// <remarks>The CSV must have a header record.</remarks>
    /// <typeparam name="TValue">Value parsed</typeparam>
    /// <param name="index">0-based field index</param>
    /// <param name="value">Parsed value, if successful</param>
    /// <returns><see langword="true"/> if the value was successfully parsed</returns>
    bool TryGetValue<TValue>(int index, [MaybeNullWhen(false)] out TValue value);

    /// <summary>
    /// Attempts to parse a <typeparamref name="TValue"/> from field at the specified column.
    /// </summary>
    /// <remarks>The CSV must have a header record.</remarks>
    /// <typeparam name="TValue">Value parsed</typeparam>
    /// <param name="name">Header name to get the field for</param>
    /// <param name="value">Parsed value, if successful</param>
    /// <returns><see langword="true"/> if the value was successfully parsed</returns>
    bool TryGetValue<TValue>(string name, [MaybeNullWhen(false)] out TValue value);

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

    /// <summary>
    /// Parses a value of type <typeparamref name="TValue"/> from field at the specified column.
    /// </summary>
    /// <remarks>The CSV must have a header record.</remarks>
    /// <typeparam name="TValue">Value parsed</typeparam>
    /// <param name="name">Header name to get the field for</param>
    /// <returns>Parsed value</returns>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="InvalidOperationException"/>
    /// <exception cref="CsvParserMissingException"/>
    /// <exception cref="CsvParseException"/>
    TValue GetField<TValue>(string name);

    /// <summary>
    /// Parses the current record into an instance of <typeparamref name="TRecord"/>.
    /// </summary>
    TRecord ParseRecord<TRecord>();
}

