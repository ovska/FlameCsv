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
    /// <summary>
    /// The options-instance associated with the current CSV.
    /// </summary>
    CsvOptions<T> Options { get; }

    /// <inheritdoc cref="GetField(CsvFieldIdentifier)"/>
    ReadOnlyMemory<T> this[CsvFieldIdentifier id] { get; }

    /// <summary>
    /// Returns true if the record contains the specified field.
    /// </summary>
    bool Contains(CsvFieldIdentifier id);

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
    /// <param name="id">Field index of name</param>
    /// <returns>Field value, unescaped and stripped of quotes when applicable</returns>
    ReadOnlyMemory<T> GetField(CsvFieldIdentifier id);

    /// <summary>
    /// Returns the number of fields in the current record.
    /// </summary>
    int FieldCount { get; }

    /// <summary>
    /// Attempts to parse a <typeparamref name="TValue"/> from a specific field.
    /// </summary>
    /// <typeparam name="TValue">Value parsed</typeparam>
    /// <param name="id">Field index of name</param>
    /// <param name="value">Parsed value, if successful</param>
    /// <returns><see langword="true"/> if the value was successfully parsed</returns>
    /// <exception cref="ArgumentException">The ID points to a field that does not exist</exception>
    /// <exception cref="CsvConverterMissingException">Converter not found for <typeparamref name="TValue"/></exception>
    [RUF(Messages.ConverterOverload), RDC(Messages.ConverterOverload)]
    bool TryParseField<TValue>(CsvFieldIdentifier id, [MaybeNullWhen(false)] out TValue value);

    /// <summary>
    /// Attempts to parse a <typeparamref name="TValue"/> from a specific field.
    /// </summary>
    /// <typeparam name="TValue">Value parsed</typeparam>
    /// <param name="converter">Converter to parse the field with</param>
    /// <param name="id">Field index of name</param>
    /// <param name="value">Parsed value, if successful</param>
    /// <returns><see langword="true"/> if the value was successfully parsed</returns>
    /// <exception cref="ArgumentException">The ID points to a field that does not exist</exception>
    /// <exception cref="CsvConverterMissingException">Converter not found for <typeparamref name="TValue"/></exception>
    bool TryParseField<TValue>(
        CsvConverter<T, TValue> converter,
        CsvFieldIdentifier id,
        [MaybeNullWhen(false)] out TValue value);

    /// <summary>
    /// Parses a value of type <typeparamref name="TValue"/> from a specific field.
    /// </summary>
    /// <typeparam name="TValue">Value parsed</typeparam>
    /// <param name="id">Field index of name</param>
    /// <returns>Parsed value</returns>
    /// <exception cref="ArgumentException">The ID points to a field that does not exist</exception>
    /// <exception cref="CsvConverterMissingException">Converter not found for <typeparamref name="TValue"/></exception>
    /// <exception cref="CsvParseException">The field value could not be parsed</exception>
    [RUF(Messages.ConverterOverload), RDC(Messages.ConverterOverload)]
    TValue ParseField<TValue>(CsvFieldIdentifier id);

    /// <summary>
    /// Parses a value of type <typeparamref name="TValue"/> from a specific field.
    /// </summary>
    /// <typeparam name="TValue">Value parsed</typeparam>
    /// <param name="converter">Converter to parse the field with</param>
    /// <param name="id">Field index of name</param>
    /// <returns>Parsed value</returns>
    /// <exception cref="ArgumentException">The ID points to a field that does not exist</exception>
    /// <exception cref="CsvConverterMissingException">Converter not found for <typeparamref name="TValue"/></exception>
    /// <exception cref="CsvParseException">The field value could not be parsed</exception>
    TValue ParseField<TValue>(CsvConverter<T, TValue> converter, CsvFieldIdentifier id);

    /// <summary>
    /// Parses the record into an instance of <typeparamref name="TRecord"/> using reflection.
    /// </summary>
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    TRecord ParseRecord<[DAM(Messages.ReflectionBound)] TRecord>();

    /// <summary>
    /// Parses the record into an instance of <typeparamref name="TRecord"/> without reflection.
    /// </summary>
    TRecord ParseRecord<TRecord>(CsvTypeMap<T, TRecord> typeMap);
}
