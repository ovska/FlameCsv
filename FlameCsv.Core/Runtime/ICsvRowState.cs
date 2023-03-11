using FlameCsv.Reading;

namespace FlameCsv.Runtime;

/// <summary>
/// State of a CSV row being parsed.
/// </summary>
/// <remarks>
/// Disposing the state resets possible leftover object references from partially parsed lines.
/// </remarks>
internal interface ICsvRowState<T, out TResult> : IDisposable where T : unmanaged, IEquatable<T>
{
    /// <summary>Amount of columns required to create a value.</summary>
    int ColumnCount { get; }

    /// <summary>
    /// Parses <typeparamref name="TResult"/> from the CSV columns.
    /// </summary>
    /// <param name="enumerator">Column enumerator</param>
    /// <returns>Parsed value</returns>
    /// <exception cref="Exceptions.CsvFormatException">
    /// Thrown if the data is invalid (e.g. wrong column count)
    /// </exception>
    /// <exception cref="Exceptions.CsvParseException">Thrown if a value cannot be parsed</exception>
    TResult Parse(ref CsvColumnEnumerator<T> enumerator);
}
