using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace FlameCsv.Reading.Internal;

#pragma warning disable RCS1158 // Static member in generic type should use a type parameter

internal interface ISimdCalculator<T, TVector>
    where T : unmanaged, INumber<T>
    where TVector : struct
{
    static abstract bool IsValid { get; }
    static abstract int Count { get; }
    static abstract TVector Create(T value);
    static abstract TVector Create(ref T value);
    static abstract TVector Equals(TVector left, TVector right);
    static abstract bool EqualsAny(TVector left, TVector right);
    static abstract bool EqualsAll(TVector left, TVector right);
    static abstract bool IsZero(TVector left);
    static abstract int GetMask(TVector value);
    static abstract TVector Or(TVector left, TVector right);
    static abstract TNum SumNonZero<TNum>(TVector value) where TNum : INumber<TNum>;
}

internal readonly struct SimdVector128<T> : ISimdCalculator<T, Vector128<T>>
    where T : unmanaged, INumber<T>
{
    public static bool IsValid { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Vector128.IsHardwareAccelerated && Vector128<T>.IsSupported; }
    public static int Count { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Vector128<T>.Count; }

    public Vector128<T> Quote { get; }
    public Vector128<T> Newline { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<T> Equals(Vector128<T> left, Vector128<T> right) => Vector128.Equals(left, right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsZero(Vector128<T> left) => left == Vector128<T>.Zero;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<T> Create(T value) => Vector128.Create(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<T> Create(ref T value) => Vector128.LoadUnsafe(ref value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsAny(Vector128<T> left, Vector128<T> right) => Vector128.EqualsAny(left, right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsAll(Vector128<T> left, Vector128<T> right) => Vector128.EqualsAll(left, right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetMask(Vector128<T> value) => (int)uint.TrailingZeroCount(value.ExtractMostSignificantBits());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<T> Or(Vector128<T> left, Vector128<T> right) => left | right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TNum SumNonZero<TNum>(Vector128<T> value) where TNum : INumber<TNum> => TNum.CreateSaturating(-Vector128.Sum(value));
}

internal readonly struct SimdVector256<T> : ISimdCalculator<T, Vector256<T>>
    where T : unmanaged, INumber<T>
{
    public static bool IsValid { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Vector256.IsHardwareAccelerated && Vector256<T>.IsSupported; }
    public static int Count { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Vector256<T>.Count; }

    public Vector256<T> Quote { get; }
    public Vector256<T> Newline { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<T> Equals(Vector256<T> left, Vector256<T> right) => Vector256.Equals(left, right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsZero(Vector256<T> left) => left == Vector256<T>.Zero;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<T> Create(T value) => Vector256.Create(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<T> Create(ref T value) => Vector256.LoadUnsafe(ref value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsAny(Vector256<T> left, Vector256<T> right) => Vector256.EqualsAny(left, right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsAll(Vector256<T> left, Vector256<T> right) => Vector256.EqualsAll(left, right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetMask(Vector256<T> value) => (int)uint.TrailingZeroCount(value.ExtractMostSignificantBits());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<T> Or(Vector256<T> left, Vector256<T> right) => left | right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TNum SumNonZero<TNum>(Vector256<T> value) where TNum : INumber<TNum> => TNum.CreateSaturating(-Vector256.Sum(value));
}

[Obsolete]
internal readonly struct SimdVector512<T> : ISimdCalculator<T, Vector512<T>> where T : unmanaged, INumber<T>
{
    public static bool IsValid { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Vector512.IsHardwareAccelerated && Vector512<T>.IsSupported; }
    public static int Count { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Vector512<T>.Count; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<T> Equals(Vector512<T> left, Vector512<T> right) => Vector512.Equals(left, right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsZero(Vector512<T> left) => left == Vector512<T>.Zero;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<T> Create(T value) => Vector512.Create(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<T> Create(ref T value) => Vector512.LoadUnsafe(ref value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsAny(Vector512<T> left, Vector512<T> right) => Vector512.EqualsAny(left, right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsAll(Vector512<T> left, Vector512<T> right) => Vector512.EqualsAll(left, right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetMask(Vector512<T> value) => (int)ulong.TrailingZeroCount(value.ExtractMostSignificantBits());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<T> Or(Vector512<T> left, Vector512<T> right) => left | right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TNum SumNonZero<TNum>(Vector512<T> value) where TNum : INumber<TNum> => TNum.CreateSaturating(-Vector512.Sum(value));
}
