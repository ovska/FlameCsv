using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Benchmark;

public class HasEOLBench
{
    [Params(7, 10, 20)] public int Frequency { get; set; }

    [Benchmark(Baseline =true)]
    public void HasEOL()
    {
        Meta[] array = Frequency switch
        {
            7 => _f7,
            10 => _f10,
            20 => _f20,
            _ => [],
        };

        ref Meta meta = ref MemoryMarshal.GetArrayDataReference(array);
        int remaining = array.Length - 1;

        while (Meta.TryFindNextEOL(ref meta, remaining, out int index))
        {
            remaining -= index;
            meta = ref Unsafe.Add(ref meta, index);
        }
    }
    
    [Benchmark]
    public void Simd()
    {
        Meta[] array = Frequency switch
        {
            7 => _f7,
            10 => _f10,
            20 => _f20,
            _ => [],
        };

        ref Meta meta = ref MemoryMarshal.GetArrayDataReference(array);
        int remaining = array.Length - 1;

        while (Meta.TryFindNextEOLSIMD(ref meta, remaining, out int index))
        {
            remaining -= index;
            meta = ref Unsafe.Add(ref meta, index);
        }
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        foreach (var freq in (int[]) [7, 10, 20])
        {
            var array = freq switch
            {
                7 => _f7,
                10 => _f10,
                20 => _f20,
                _ => throw new ArgumentOutOfRangeException(nameof(freq))
            };

            for (int i = 0; i < array.Length; i += freq)
            {
                array[i] = Meta.EOL(i, 0, 0);
            }
        }
    }

    private readonly Meta[] _f7 = new Meta[2000];
    private readonly Meta[] _f10 = new Meta[2000];
    private readonly Meta[] _f20 = new Meta[2000];
}
