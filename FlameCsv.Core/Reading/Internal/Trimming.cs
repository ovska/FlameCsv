using System.Runtime.CompilerServices;

namespace FlameCsv.Reading.Internal;

internal interface ITrimmer
{
    static abstract void Trim<T>(ref ReadOnlySpan<T> span)
        where T : unmanaged, IBinaryInteger<T>;
}

internal readonly struct NoTrimming : ITrimmer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Trim<T>(ref ReadOnlySpan<T> span)
        where T : unmanaged, IBinaryInteger<T>
    {
        // no trimming
    }
}

internal readonly struct LeadingTrimming : ITrimmer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Trim<T>(ref ReadOnlySpan<T> span)
        where T : unmanaged, IBinaryInteger<T>
    {
        int start;

        for (start = 0; start < span.Length; start++)
        {
            if (span[start] != T.CreateTruncating(' '))
            {
                break;
            }
        }

        span = span.Slice(start);
    }
}

internal readonly struct TrailingTrimming : ITrimmer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Trim<T>(ref ReadOnlySpan<T> span)
        where T : unmanaged, IBinaryInteger<T>
    {
        int end;

        for (end = span.Length - 1; end >= 0; end--)
        {
            if (span[end] != T.CreateTruncating(' '))
            {
                break;
            }
        }

        span = span.Slice(0, end);
    }
}

internal readonly struct LeadingAndTrailingTrimming : ITrimmer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Trim<T>(ref ReadOnlySpan<T> span)
        where T : unmanaged, IBinaryInteger<T>
    {
        int start;
        int end;

        for (start = 0; start < span.Length; start++)
        {
            if (span[start] != T.CreateTruncating(' '))
            {
                break;
            }
        }

        for (end = span.Length - 1; end >= start; end--)
        {
            if (span[end] != T.CreateTruncating(' '))
            {
                break;
            }
        }

        span = span.Slice(start, end - start + 1);
    }
}
