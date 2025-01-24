using System.Diagnostics;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace FlameCsv.Reading.Internal;

/// <summary>
/// Interface to provide high-performance generic handling for variable length newlines.
/// </summary>
internal interface INewline<T> where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Returns the offset required in the search space to be able to read the whole newline value.
    /// </summary>
    static abstract nuint OffsetFromEnd { get; }

    /// <summary>
    /// Clears the second bit in the mask if needed. No-op for single newline sequences.
    /// </summary>
    /// <param name="mask">The mask to modify.</param>
    static abstract void ClearSecondBitIfNeeded(ref nuint mask);

    /// <summary>
    /// Determines if the specified value represents a newline.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <remarks>
    /// Possibly reads the next value in the sequence, see <see cref="OffsetFromEnd"/>.
    /// </remarks>
    /// <returns>True if the value represents a newline; otherwise, false.</returns>
    [Pure]
    bool IsNewline(ref T value);

    /// <summary>
    /// Determines if the specified value represents a delimiter or a newline.
    /// </summary>
    /// <param name="delimiter">Delimiter</param>
    /// <param name="value">The value to check against</param>
    /// <param name="isEOL">When true, whether the value was a newline instead of delimiter</param>
    /// <returns>True if the value represents a delimiter or a newline; otherwise, false.</returns>
    /// <remarks>For single token newlines, always returns true</remarks>
    [Pure]
    bool IsDelimiterOrNewline(T delimiter, ref T value, out bool isEOL);
}

internal interface INewlineParser<T, TVector> : INewline<T>
    where T : unmanaged, IBinaryInteger<T>
    where TVector : struct
{
    /// <summary>
    /// Determines if the input vector contains any token of the newline.
    /// </summary>
    /// <param name="input">The input vector to check.</param>
    /// <returns>A vector indicating the positions of newline sequences.</returns>
    [Pure]
    TVector HasNewline(TVector input);
}

[SkipLocalsInit]
internal readonly ref struct NewlineParserOne<T, TVector>(T first) : INewlineParser<T, TVector>
    where T : unmanaged, IBinaryInteger<T>
    where TVector : struct, ISimdVector<T, TVector>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TVector HasNewline(TVector input) => TVector.Equals(input, _firstVec);

    private readonly TVector _firstVec = TVector.Create(first);

    public static nuint OffsetFromEnd
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsNewline(ref T value) => value == first;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsDelimiterOrNewline(T delimiter, ref T value, out bool isEOL)
    {
        Debug.Assert(value == delimiter || value == first);
        isEOL = value != delimiter;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ClearSecondBitIfNeeded(ref nuint mask)
    {
        // no-op
    }
}

[SkipLocalsInit]
internal readonly ref struct NewlineParserTwo<T, TVector>(T first, T second) : INewlineParser<T, TVector>
    where T : unmanaged, IBinaryInteger<T>
    where TVector : struct, ISimdVector<T, TVector>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TVector HasNewline(TVector input)
        => TVector.Or(TVector.Equals(input, _firstVec), TVector.Equals(input, _secondVec));

    private readonly TVector _firstVec = TVector.Create(first);
    private readonly TVector _secondVec = TVector.Create(second);

    public static nuint OffsetFromEnd
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => 1;
    }

    // profiled: this is faster than comparing to a combined uint/ushort value
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsNewline(ref T value) => value == first && Unsafe.Add(ref value, 1) == second;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsDelimiterOrNewline(T delimiter, ref T value, out bool isEOL)
    {
        if (delimiter == value)
        {
            isEOL = false;
            return true;
        }

        isEOL = true;
        return value == first && Unsafe.Add(ref value, 1) == second;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ClearSecondBitIfNeeded(ref nuint mask)
    {
        // only works properly if IsDelimiterOrNewline check is valid
        mask &= (mask - 1);
    }
}
