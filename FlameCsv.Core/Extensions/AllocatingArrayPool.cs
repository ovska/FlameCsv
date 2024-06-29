using System.Buffers;
using System.Numerics;
using CommunityToolkit.Diagnostics;

namespace FlameCsv.Extensions;

/// <summary>
/// Array "pool" that always allocates a new array.
/// </summary>
internal sealed class AllocatingArrayPool<T> : ArrayPool<T>
{
    [Obsolete("Use Instance, not Shared", true)]
    public static new ArrayPool<T> Shared => throw new System.Diagnostics.UnreachableException();

    /// <inheritdoc cref="AllocatingArrayPool{T}"/>
    public static AllocatingArrayPool<T> Instance { get; } = new();

    private AllocatingArrayPool() { }

    public override T[] Rent(int minimumLength)
    {
        Guard.IsGreaterThanOrEqualTo(minimumLength, 0);
        return minimumLength > 0
            ? new T[BitOperations.RoundUpToPowerOf2((uint)minimumLength)]
            : [];
    }

    public override void Return(T[] array, bool clearArray = false)
    {
        ArgumentNullException.ThrowIfNull(array);
    }
}
