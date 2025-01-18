namespace FlameCsv.Writing;

/// <summary>
/// Provides tokens and methods to escape special characters when quoting fields.
/// </summary>
/// <seealso cref="CsvDialect{T}.NeedsQuoting"/>
internal interface IEscaper<T> where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Gets the escape character, can be same as <see cref="Quote"/>.
    /// </summary>
    T Escape { get; }

    /// <summary>
    /// Gets the string delimiter.
    /// </summary>
    T Quote { get; }

    /// <summary>
    /// Returns the last index of characters that need escaping in <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The span to search for escapable characters.</param>
    /// <returns>The last index of escapable characters, or -1 if none are found.</returns>
    int LastIndexOfEscapable(scoped ReadOnlySpan<T> value);

    /// <summary>
    /// Counts the number of special characters in the span. Using RFC4180 mode, this counts quotes.
    /// In unix mode, counts both quotes and escapes.
    /// </summary>
    /// <param name="field">The span to search for special characters.</param>
    /// <returns>The number of special characters found.</returns>
    /// <remarks>Called after <see cref="CsvDialect{T}.NeedsQuoting"/> matches a token.</remarks>
    int CountEscapable(scoped ReadOnlySpan<T> field);

    /// <summary>
    /// Returns <see langword="true"/> if the value needs escaping.
    /// </summary>
    /// <param name="value">The value to check for escaping.</param>
    /// <returns><c>true</c> if the value needs escaping; otherwise, <c>false</c>.</returns>
    bool NeedsEscaping(T value);

    /// <summary>
    /// Gets a value indicating whether vectorization is supported (all tokens are ASCII).
    /// </summary>
    bool SupportsVectorization { get; }
}
