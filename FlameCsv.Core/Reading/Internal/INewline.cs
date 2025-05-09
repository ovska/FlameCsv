﻿using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using JetBrains.Annotations;

namespace FlameCsv.Reading.Internal;

/// <summary>
/// Interface to provide high-performance generic handling for variable length newlines.
/// </summary>
internal interface INewline<T> where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Returns the length of the newline sequence.
    /// </summary>
    static abstract int GetLength(bool isMultitoken);

    /// <summary>
    /// Returns the offset required in the search space to be able to read the whole newline value.<br/>
    /// This is always <c>0</c> or <c>1</c>.
    /// </summary>
    static abstract nuint OffsetFromEnd { get; }

    /// <summary>
    /// Determines if the specified value is part of a two-token newline sequence.
    /// </summary>
    /// <remarks>For single token newlines, always returns false</remarks>
    [Pure]
    bool IsMultitoken(ref T value);

    /// <summary>
    /// Determines if the specified value represents a delimiter or a newline.
    /// </summary>
    /// <param name="value">The value to check against</param>
    /// <param name="isMultitoken">When true, whether the next token was part of the newline as well.</param>
    /// <returns>True if the value represents a newline instead of a delimiter.</returns>
    /// <remarks>For single token newlines, always returns true</remarks>
    [Pure]
    bool IsNewline(ref T value, out bool isMultitoken);

    /// <summary>The first token in the newline.</summary>
    T First { [Pure] get; }

    /// <summary>The second token in the newline, or <see cref="First"/> if length is 1.</summary>
    T Second { [Pure] get; }
}

/// <inheritdoc/>
internal interface INewline<T, TVector> : INewline<T>
    where T : unmanaged, IBinaryInteger<T>
    where TVector : struct
{
    /// <summary>
    /// Loads the newline vectors.
    /// </summary>
    [Pure]
    void Load(out TVector v0, out TVector v1);

    /// <summary>
    /// Checks if the input vector contains a newline.
    /// </summary>
    static abstract TVector HasNewline(TVector input, TVector v0, TVector v1);
}

[SkipLocalsInit]
internal readonly struct NewlineParserOne<T, TVector>(T first) : INewline<T, TVector>
    where T : unmanaged, IBinaryInteger<T>
    where TVector : struct, ISimdVector<T, TVector>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetLength(bool isMultitoken) => 1;

    public static nuint OffsetFromEnd
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsMultitoken(ref T value)
    {
        // single token newlines are never multitoken
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsNewline(ref T value, out bool isMultitoken)
    {
        // the HasNewline vector only contains the correct values, e.g., \n, so this check should always succeed
        isMultitoken = false;
        return value == first;
    }

    public T First => first;
    public T Second => first;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Load(out TVector v0, out TVector v1)
    {
        v0 = TVector.Create(first);
        Unsafe.SkipInit(out v1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TVector HasNewline(TVector input, TVector v0, TVector v1)
    {
        return TVector.Equals(input, v0);
    }
}

[SkipLocalsInit]
internal readonly struct NewlineParserTwo<T, TVector>(T first, T second) : INewline<T, TVector>
    where T : unmanaged, IBinaryInteger<T>
    where TVector : struct, ISimdVector<T, TVector>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetLength(bool isMultitoken) => 1 + isMultitoken.ToByte();

    public static nuint OffsetFromEnd
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => 1;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsMultitoken(ref T value)
    {
        // only \r\n is considered a multitoken newline, other combinations e.g. \n\n are two distinct newlines
        return value == first && Unsafe.Add(ref value, 1) == second;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsNewline(ref T value, out bool isMultitoken)
    {
        if (value == first || value == second)
        {
            isMultitoken = value == first && Unsafe.Add(ref value, 1) == second;
            return true;
        }

        isMultitoken = false;
        return false;
    }

    public T First => first;
    public T Second => second;

    public void Load(out TVector v0, out TVector v1)
    {
        v0 = TVector.Create(first);
        v1 = TVector.Create(second);
    }

    public static TVector HasNewline(TVector input, TVector v0, TVector v1)
    {
        return TVector.Equals(input, v0) | TVector.Equals(input, v1);
    }
}
