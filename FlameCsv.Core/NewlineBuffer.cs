using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Extensions;

namespace FlameCsv;

/// <summary>
/// Internal implementation detail.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly struct NewlineBuffer<T> : IEquatable<NewlineBuffer<T>> where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Returns a newline buffer with length of 2 and value of <c>\r\n</c>.
    /// </summary>
    public static NewlineBuffer<T> CRLF
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(2, T.CreateChecked('\r'), T.CreateChecked('\n'));
    }

    /// <summary>
    /// Returns a newline buffer with length of 1 and value of <c>\n</c>.
    /// </summary>
    public static NewlineBuffer<T> LF
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(1, T.CreateChecked('\n'), T.CreateChecked('\n'));
    }

    /// <summary>
    /// Returns <c>true</c> if the newline is <see langword="default"/>; otherwise, <c>false</c>.
    /// </summary>
    public bool IsEmpty => Length == 0;

    /// <summary>Newline length.</summary>
    public readonly int Length;

    /// <summary>First character of the newline.</summary>
    public readonly T First;

    /// <summary>Second character of the newline. If length is 1, this is the same as <see cref="First"/>.</summary>
    public readonly T Second;

    /// <summary>
    /// Initializes a 1-character newline.
    /// </summary>
    /// <param name="first"></param>
    /// <remarks>
    /// Does not validate the character's validity, see <see cref="CsvDialect{T}.Validate"/>.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NewlineBuffer(T first)
    {
        (Length, First, Second) = (1, first, first);
    }

    /// <summary>
    /// Initializes a 2-character newline.
    /// </summary>
    /// <param name="first">First character</param>
    /// <param name="second">Second character; must not be the same as <paramref name="first"/>.</param>
    /// <remarks>
    /// Does not validate the character's validity, see <see cref="CsvDialect{T}.Validate"/>.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NewlineBuffer(T first, T second)
    {
        if (first == second) Throw.Argument(nameof(second), "Newline cannot contain two of the same character");
        (Length, First, Second) = (2, first, second);
    }

    // perf: the throw check isn't elided on CRLF and LF for the ctor above, so use a private one
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private NewlineBuffer(int length, T first, T second)
    {
        Debug.Assert(length == 2 || (length == 1 && first == second));
        First = first;
        Second = second;
        Length = length;
    }

    [Conditional("DEBUG")]
    internal void AssertInitialized() => Debug.Assert(Length != 0);

    /// <inheritdoc />
    public bool Equals(NewlineBuffer<T> other)
    {
        return Length == other.Length && First == other.First && Second == other.Second;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is NewlineBuffer<T> other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(Length, First, Second);
    }

    /// <summary></summary>
    public static bool operator ==(NewlineBuffer<T> left, NewlineBuffer<T> right)
    {
        return left.Equals(right);
    }

    /// <summary></summary>
    public static bool operator !=(NewlineBuffer<T> left, NewlineBuffer<T> right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Creates an array from the newline buffer.
    /// </summary>
    public T[] ToArray()
    {
        var result = new T[Length];

        if (Length > 0)
        {
            result[0] = First;

            if (Length == 2)
            {
                result[1] = Second;
            }
        }

        return result;
    }
}
