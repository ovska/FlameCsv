namespace FlameCsv.Writing;

internal interface IEscaper<T> where T : unmanaged, IEquatable<T>
{
    T Escape { get; }
    T Quote { get; }
    int CountEscapable(ReadOnlySpan<T> value);
    bool NeedsEscaping(ReadOnlySpan<T> value, out int specialCount);
    bool NeedsEscaping(T value);
}
