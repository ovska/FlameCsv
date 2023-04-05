using System.Buffers;
using CommunityToolkit.Diagnostics;

namespace FlameCsv.Extensions;

/// <summary>
/// Array "pool" that always allocates a new array.
/// </summary>
internal sealed class AllocatingArrayPool<T> : ArrayPool<T> where T : unmanaged
{
    /// <inheritdoc cref="AllocatingArrayPool{T}"/>
    public static AllocatingArrayPool<T> Instance { get; } = new();

    private AllocatingArrayPool() { }

    public override T[] Rent(int minimumLength)
    {
        Guard.IsGreaterThanOrEqualTo(minimumLength, 0);
        return minimumLength > 0 ? new T[minimumLength] : Array.Empty<T>();
    }

    public override void Return(T[] array, bool clearArray = false)
    {
        // unnecessary?
        //if (clearArray) array.AsSpan().Clear();
    }
}
