using System.Diagnostics.CodeAnalysis;
using FlameCsv.Binding;
using FlameCsv.Exceptions;
using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// An instance representing a single CSV record.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
/// <seealso cref="CsvValueRecord{T}"/>
/// <seealso cref="CsvRecord{T}"/>
[PublicAPI]
public interface ICsvRecord<T> where T : unmanaged, IBinaryInteger<T>
{
    /// <inheritdoc cref="GetField(int)"/>
    ReadOnlyMemory<T> this[int index] { get; }

    /// <inheritdoc cref="GetField(string)"/>
    ReadOnlyMemory<T> this[string name] { get; }

    /// <summary>
    /// Returns the header record for the current CSV. Throws if <see cref="HasHeader"/> is <see langword="false"/>.
    /// </summary>
    /// <seealso cref="HasHeader"/>
    /// <exception cref="NotSupportedException">Options is configured not to have a header</exception>
    ReadOnlySpan<string> Header { get; }

    /// <summary>
    /// Returns true if the header has been parsed from the CSV the record and <see cref="Header"/> is safe to use.
    /// </summary>
    /// <remarks>
    /// The header isn't returned as a separate record, so this property is always true if the options-instance
    /// is configured to have a header, and always false if not.
    /// </remarks>
    bool HasHeader { get; }

    /// <summary>
    /// 0-based byte/character position of the record in the data.
    /// </summary>
    long Position { get; }

    /// <summary>
    /// 1-based line number in the CSV data. Empty lines and the header are counted.
    /// </summary>
    int Line { get; }

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
    /// <param name="index">0-based field index, e.g., 0 for the first field</param>
    /// <returns>Field value, unescaped and stripped of quotes when applicable</returns>
    /// <exception cref="ArgumentOutOfRangeException"/>
    ReadOnlyMemory<T> GetField(int index);

    /// <summary>
    /// Returns the value of the field with the specified name. Requires for the CSV to have a header record.
    /// </summary>
    /// <remarks>The CSV must have a header record, see <see cref="HasHeader"/>.</remarks>
    /// <param name="name">Header name to get the field for</param>
    /// <returns>Field value, unescaped and stripped of quotes when applicable</returns>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="InvalidOperationException"/>
    ReadOnlyMemory<T> GetField(string name);

    /// <summary>
    /// Returns the number of fields in the current record.
    /// </summary>
    int GetFieldCount();

    /// <summary>
    /// Attempts to parse a <typeparamref name="TValue"/> from field at <paramref name="index"/>.
    /// </summary>
    /// <typeparam name="TValue">Value parsed</typeparam>
    /// <param name="index">0-based field index</param>
    /// <param name="value">Parsed value, if successful</param>
    /// <returns><see langword="true"/> if the value was successfully parsed</returns>
    bool TryGetValue<TValue>(int index, [MaybeNullWhen(false)] out TValue value);

    /// <summary>
    /// Attempts to parse a <typeparamref name="TValue"/> from field at the specified field.
    /// </summary>
    /// <remarks>The CSV must have a header record, see <see cref="HasHeader"/>.</remarks>
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
    /// <exception cref="CsvConverterMissingException"/>
    /// <exception cref="CsvParseException"/>
    TValue GetField<TValue>(int index);

    /// <summary>
    /// Parses a value of type <typeparamref name="TValue"/> from field at the specified field.
    /// </summary>
    /// <remarks>The CSV must have a header record, see <see cref="HasHeader"/>.</remarks>
    /// <typeparam name="TValue">Value parsed</typeparam>
    /// <param name="name">Header name to get the field for</param>
    /// <returns>Parsed value</returns>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="InvalidOperationException"/>
    /// <exception cref="CsvConverterMissingException"/>
    /// <exception cref="CsvParseException"/>
    TValue GetField<TValue>(string name);

    /// <summary>
    /// Parses the record into an instance of <typeparamref name="TRecord"/> using reflection.
    /// </summary>
    [RUF(Messages.CompiledExpressions), RDC(Messages.CompiledExpressions)]
    TRecord ParseRecord<[DAM(Messages.ReflectionBound)] TRecord>();

    /// <summary>
    /// Parses the record into an instance of <typeparamref name="TRecord"/> without reflection.
    /// </summary>
    /// <inheritdoc cref="ParseRecord{TRecord}()"/>
    TRecord ParseRecord<TRecord>(CsvTypeMap<T, TRecord> typeMap);
}
