using System.Buffers;

namespace FlameCsv.Readers;

/// <summary>
/// Reads values of type <typeparamref name="TValue"/> from a <see cref="ReadOnlySequence{T}"/>.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
/// <typeparam name="TValue">Parsed value</typeparam>
internal interface ICsvProcessor<T, TValue> : IDisposable where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Consumes (slices) the buffer and attempts to parse instances of <typeparamref name="TValue"/> from it.
    /// </summary>
    /// <remarks>
    /// The buffer might be consumed despite no values being parsed, e.g. when skipping rows, parsing the header,
    /// or detecting delimiters.
    /// </remarks>
    /// <param name="buffer">Buffer the data is read from</param>
    /// <param name="value">Parsed value</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> was parsed and can be used</returns>
    bool TryContinueRead(ref ReadOnlySequence<T> buffer, out TValue value);

    /// <summary>
    /// Attempts to parse an instance of <typeparamref name="TValue"/> from the remaining data. This
    /// method is meant to be used for reading the last record from data that didn't have a trailing newline.
    /// </summary>
    /// <param name="remaining">Leftover buffer from <see cref="TryContinueRead"/></param>
    /// <param name="value">Parsed value</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> was parsed and can be used</returns>
    bool TryReadRemaining(in ReadOnlySequence<T> remaining, out TValue value);

}
