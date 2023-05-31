namespace FlameCsv.Writing;

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
    /// Counts the number of special characters in the span.
    /// </summary>
    int CountEscapable(ReadOnlySpan<T> value);

    /// <inheritdoc cref="NeedsEscaping(T)"/>
    bool NeedsEscaping(ReadOnlySpan<T> value, out int specialCount);

    /// <summary>
    /// Returns <see langword="true"/> if the value contains any special characters that need to be escaped/quoted.
    /// </summary>
    bool NeedsEscaping(T value);
}
