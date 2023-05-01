using System.Buffers;

namespace FlameCsv.Reading;

/// <summary>
/// Reads values of type <typeparamref name="TValue"/> from a <see cref="ReadOnlySequence{T}"/>.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
/// <typeparam name="TValue">Parsed value</typeparam>
internal interface ICsvProcessor<T, TValue> : IDisposable where T : unmanaged, IEquatable<T>
{
    int Line { get; }

    long Position { get; }

    /// <summary>
    /// Consumes (slices) the buffer and attempts to parse instances of <typeparamref name="TValue"/> from it.
    /// </summary>
    /// <remarks>
    /// The buffer might be consumed despite no values being parsed, e.g. when skipping rows, parsing the header,
    /// or detecting delimiters.
    /// </remarks>
    /// <param name="buffer">Buffer the data is read from</param>
    /// <param name="value">Parsed value</param>
    /// <param name="isFinalBlock">Whether the buffer is the final block and no newline should be seeked</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> was parsed and can be used</returns>
    bool TryRead(ref ReadOnlySequence<T> buffer, out TValue value, bool isFinalBlock);
}
