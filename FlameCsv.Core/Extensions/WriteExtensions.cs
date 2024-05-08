using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlameCsv.Extensions;

internal static class WriteExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<TTo> UnsafeCast<TFrom, TTo>(this Span<TFrom> span)
        where TFrom : unmanaged
        where TTo : unmanaged
    {
        Debug.Assert(Unsafe.SizeOf<TFrom>() == Unsafe.SizeOf<TTo>());

        return MemoryMarshal.CreateSpan(
            ref Unsafe.As<TFrom, TTo>(ref MemoryMarshal.GetReference(span)),
            span.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<TTo> UnsafeCast<TFrom, TTo>(this ReadOnlySpan<TFrom> span)
    where TFrom : unmanaged
    where TTo : unmanaged
    {
        Debug.Assert(Unsafe.SizeOf<TFrom>() == Unsafe.SizeOf<TTo>());

        return MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.As<TFrom, TTo>(ref MemoryMarshal.GetReference(span)),
            span.Length);
    }

    /// <summary>
    /// Attempts to copy the contents of <paramref name="value"/> to <paramref name="buffer"/>.
    /// </summary>
    /// <param name="value">Value to copy, can be empty</param>
    /// <param name="buffer">Destination buffer</param>
    /// <param name="tokensWritten">Length of <paramref name="value"/> if the copy succeeded</param>
    /// <typeparam name="T">Token type</typeparam>
    /// <returns>True if the destination buffer is large enough and data was copied.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryWriteTo<T>(
        this Span<T> value,
        Span<T> buffer,
        out int tokensWritten)
    {
        if (value.TryCopyTo(buffer))
        {
            tokensWritten = value.Length;
            return true;
        }

        tokensWritten = 0;
        return false;
    }

    /// <inheritdoc cref="TryWriteTo{T}(Span{T},Span{T},out int)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryWriteTo<T>(
        this ReadOnlySpan<T> value,
        Span<T> buffer,
        out int tokensWritten)
    {
        if (value.TryCopyTo(buffer))
        {
            tokensWritten = value.Length;
            return true;
        }

        tokensWritten = 0;
        return false;
    }
}
