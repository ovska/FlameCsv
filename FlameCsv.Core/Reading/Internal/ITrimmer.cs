using System.Runtime.CompilerServices;

namespace FlameCsv.Reading.Internal;

internal interface ITrimmer<in T> where T : unmanaged, IEquatable<T>
{
    bool Check(T value);
}

internal static class Trimmer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> Trim<T, TTrimmer>(TTrimmer trimmer, ReadOnlySpan<T> span)
        where T : unmanaged, IEquatable<T>
        where TTrimmer : struct, ITrimmer<T>, allows ref struct
    {
        int start = 0;

        for (; start < span.Length; start++)
        {
            if (!trimmer.Check(span[start])) break;
        }

        int end = span.Length - 1;

        for (; end >= start; end--)
        {
            if (!trimmer.Check(span[end])) break;
        }

        return span.Slice(start, end - start + 1);
    }
}

internal readonly struct SingleTrimmer<T>(T first) : ITrimmer<T> where T : unmanaged, IEquatable<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Check(T value) => value.Equals(first);
}

internal readonly struct DoubleTrimmer<T>(T first, T second) : ITrimmer<T> where T : unmanaged, IEquatable<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Check(T value) => value.Equals(first) || value.Equals(second);
}

internal readonly ref struct AnyTrimmer<T>(ReadOnlySpan<T> values) : ITrimmer<T> where T : unmanaged, IEquatable<T>
{
    private readonly ReadOnlySpan<T> _values = values;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Check(T value)
    {
        for (int i = 0; i < _values.Length; i++)
        {
            if (_values[i].Equals(value)) return true;
        }

        return false;
    }
}
