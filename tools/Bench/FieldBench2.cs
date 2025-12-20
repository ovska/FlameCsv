using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Benchmark;

public class FieldBench2
{
    private const int Length = 14 * 1024 * 64;
    private readonly string _data = new string('x', Length);

    private readonly uint[] _fields;
    private readonly ulong[] _bits;

    [Benchmark(Baseline = true)]
    public void FromBits()
    {
        ref char dataRef = ref _data.DangerousGetReference();

        for (int i = 0; i < _bits.Length; i++)
        {
            ulong bits = _bits[i];
            uint start = (uint)bits;
            uint end = (uint)(bits >> 32);

            if ((long)bits < 0)
            {
                _ = Slow(bits);
            }
            else
            {
                _ = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref dataRef, start), (int)(end - start));
            }
        }
    }

    [Benchmark]
    public void FromPairs()
    {
        ref char dataRef = ref _data.DangerousGetReference();
        ref uint f = ref _fields[0];

        for (int i = 1; i < _fields.Length; i++)
        {
            ref uint start = ref Unsafe.Add(ref f, (nuint)i - 1);
            uint end = Unsafe.Add(ref f, (nuint)i);
            uint s = (uint)Field.NextStart(start);
            uint e = (uint)Field.End(end);

            if ((int)end < 0)
            {
                _ = Slow(end);
            }
            else
            {
                _ = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref dataRef, s), (int)((uint)e - (uint)s));
            }
        }
    }

    [Benchmark]
    public void FromPairs2()
    {
        ref char dataRef = ref _data.DangerousGetReference();
        ref uint f = ref _fields[0];

        for (int i = 1; i < _fields.Length; i++)
        {
            uint start = Unsafe.Add(ref f, (nuint)i - 1);
            uint end = Unsafe.Add(ref f, (nuint)i);

            uint s = (uint)Field.NextStart(start);
            uint e = (uint)Field.End(end);

            if ((int)end < 0)
            {
                _ = Slow(end);
            }
            else
            {
                _ = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref dataRef, s), (int)((uint)e - (uint)s));
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ReadOnlySpan<char> Slow(ulong _) => throw new UnreachableException();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ReadOnlySpan<char> Slow(ref uint _) => throw new UnreachableException();

    public FieldBench2()
    {
        List<uint> fieldBuilder = [];
        List<ulong> bitBuilder = [];

        int previous = 0;
        int i = 0;

        while (previous < (Length - 16))
        {
            fieldBuilder.Add((uint)previous);

            int increment = i % 3 + i % 7 + 1;

            ulong bit = (uint)previous;
            bit |= (ulong)(previous += increment) << 32;

            bitBuilder.Add(bit);

            i++;
        }

        _fields = fieldBuilder.ToArray();
        _bits = bitBuilder.ToArray();
    }
}
