using System.Buffers;

namespace FlameCsv.Readers;

/// <summary>
/// Reads CSV lines from a <see cref="ReadOnlySequence{T}"/>.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
internal interface ILineReader<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Attempts to read until a non-string wrapped <see cref="CsvParserOptions{T}.NewLine"/> is found.
    /// </summary>
    /// <param name="options">Options instance from which newline and string delimiter tokens are used</param>
    /// <param name="sequence">Source data. If a line is found, it is sliced out from the sequence</param>
    /// <param name="line">
    /// The line without trailing newline tokens. Should be ignored if the method returns <see langword="false"/>.
    /// </param>
    /// <param name="quoteCount">
    /// Count of string delimiters in <paramref name="line"/>, used when parsing the columns later on.
    /// Should be ignored if the method returns <see langword="false"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the line was successfully parsed. In this case, the line and newline tokens
    /// are sliced from <paramref name="sequence"/>.
    /// </returns>
    /// <remarks>A successful result might still be invalid CSV.</remarks>
    bool TryRead(
        in CsvParserOptions<T> options,
        ref ReadOnlySequence<T> sequence,
        out ReadOnlySequence<T> line,
        out int quoteCount);
}
