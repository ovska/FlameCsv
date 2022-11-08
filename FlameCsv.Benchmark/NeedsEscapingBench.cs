using System.Numerics;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;

namespace FlameCsv.Benchmark;

public class NeedsEscapingBench
{
    [Params("", " ", " \t")]
    public string? Whitespaces { get; set; }

    private ReadOnlyMemory<char> _ws;

    [GlobalSetup]
    public void Setup()
    {
        _ws = Whitespaces.AsMemory();
    }

    private static readonly string[] TestData = {
        "test",
        " test",
        "test ",
        "\ttest",
        "test\t",
        "test\t  ",
        "\t\t\t",
        "    ",
        " ",
        "\t",
        "fklsdiofdjsfdökjlsfdölkjfds ",
        "fklsdiofdjsfdökjlsfdölkjfds \t",
    };

    [Benchmark(Baseline = true)]
    public void Trimmer()
    {
        foreach (var str in TestData)
            _ = WithTrim(str, _ws, out _);
    }
    
    [Benchmark]
    public void Comparer()
    {
        foreach (var str in TestData)
            _ = WithCompare(str, _ws, out _);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool WithCompare<T>(
        ReadOnlySpan<T> value,
        ReadOnlyMemory<T> whitespace,
        out int escapedLength)
        where T : unmanaged, IEquatable<T>
    {
        if (!whitespace.IsEmpty)
        {
            var head = value[0];
            var tail = value[^1];
            var span = whitespace.Span;

            if ((span.Length == 1 && IOA(head, tail, span[0]))
                || (span.Length == 2 && IOA(head, tail, span[0], span[1]))
                || (span.Length == 3 && IOA(head, tail, span[0], span[1], span[2]))
                || IOA(head, tail, span))
            {
                escapedLength = value.Length + 2;
                return true;
            }
        }

        escapedLength = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IOA<T>(T head, T tail, T a0)
        where T : unmanaged, IEquatable<T>
    {
        return a0.Equals(head) || a0.Equals(tail);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IOA<T>(T head, T tail, T a0, T a1)
        where T : unmanaged, IEquatable<T>
    {
        return a0.Equals(head)
            || a0.Equals(tail)
            || a1.Equals(head)
            || a1.Equals(tail);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IOA<T>(T head, T tail, T a0, T a1, T a2)
        where T : unmanaged, IEquatable<T>
    {
        return a0.Equals(head)
            || a0.Equals(tail)
            || a1.Equals(head)
            || a1.Equals(tail)
            || a2.Equals(head)
            || a2.Equals(tail);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IOA<T>(T head, T tail, T a0, T a1, T a2, T a3)
        where T : unmanaged, IEquatable<T>
    {
        return a0.Equals(head)
            || a0.Equals(tail)
            || a1.Equals(head)
            || a1.Equals(tail)
            || a2.Equals(head)
            || a2.Equals(tail)
            || a3.Equals(head)
            || a3.Equals(tail);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IOA<T>(T head, T tail, ReadOnlySpan<T> span)
        where T : unmanaged, IEquatable<T>
    {
        foreach (var a in span)
            if (a.Equals(head) || a.Equals(tail))
                return true;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool WithTrim<T>(
        ReadOnlySpan<T> value,
        ReadOnlyMemory<T> whitespace,
        out int escapedLength)
        where T : unmanaged, IEquatable<T>
    {
        if (!whitespace.IsEmpty && value.Length != value.Trim(whitespace.Span).Length)
        {
            // only the wrapping quotes needed
            escapedLength = value.Length + 2;
            return true;
        }

        escapedLength = default;
        return false;
    }
}
