using System.Diagnostics.CodeAnalysis;
using FlameCsv.Parsers;

namespace FlameCsv.Reading;

public interface ICsvFieldReader<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Attempts to read the next field in the record.
    /// </summary>
    /// <param name="field">Unescaped field value</param>
    /// <returns><see langword="true"/> a field was read, <see langword="false"/> if the end of the record was reached.</returns>
    bool TryReadNext(out ReadOnlyMemory<T> field);

    /// <summary>
    /// Throws an exception if the known field count is not equal to <paramref name="fieldCount"/>.
    /// Does nothing if the field count is unknown.
    /// </summary>
    /// <param name="fieldCount">Expected amount of fields</param>
    void TryEnsureFieldCount(int fieldCount);

    /// <summary>
    /// Throws an exception if the end of the record has not been reached.
    /// </summary>
    /// <param name="fieldCount">Expected amount of fields</param>
    void EnsureFullyConsumed(int fieldCount);

    /// <summary>
    /// Throws an exception for a failed parse.
    /// </summary>
    /// <param name="field">Parsed field</param>
    /// <param name="parser">Parser instance, may be null</param>
    [DoesNotReturn] void ThrowParseFailed(ReadOnlyMemory<T> field, ICsvParser<T>? parser);
}
