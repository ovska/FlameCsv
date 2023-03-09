namespace FlameCsv;

/// <summary>
/// Provides values that can be used to configure parsing and formatting nulls.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public interface ICsvNullTokenProvider<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Returns the default null token.
    /// </summary>
    ReadOnlyMemory<T> Default { get; }

    /// <summary>
    /// Returns a null token to be used when parsing or formatting <paramref name="type"/> instead of <see cref="Default"/>.
    /// </summary>
    /// <param name="type">Type to get the override for</param>
    /// <param name="value">Override value</param>
    /// <returns>True if the override was found</returns>
    bool TryGetOverride(Type type, out ReadOnlyMemory<T> value);
}


