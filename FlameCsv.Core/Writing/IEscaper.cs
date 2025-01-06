namespace FlameCsv.Writing;

/// <summary>
/// Provides tokens and methods to escape special characters when quoting fields.
/// </summary>
/// <seealso cref="CsvDialect{T}.NeedsQuoting"/>
internal interface IEscaper<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Escape character.
    /// </summary>
    T Escape { get; }

    /// <summary>
    /// String delimiter.
    /// </summary>
    T Quote { get; }

    /// <summary>
    /// Returns the last index of characters that need escaping in <paramref name="value"/>.
    /// </summary>
    int LastIndexOfEscapable(scoped ReadOnlySpan<T> value);

    /// <summary>
    /// Counts the number of special characters in the span.
    /// </summary>
    /// <remarks>Called after <see cref="CsvDialect{T}.NeedsQuoting"/> matches a token.</remarks>
    int CountEscapable(scoped ReadOnlySpan<T> field);

    /// <summary>
    /// Returns <see langword="true"/> if the value needs to be escaping.
    /// </summary>
    bool NeedsEscaping(T value);
}
