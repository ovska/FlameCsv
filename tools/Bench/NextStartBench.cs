using System.Runtime.CompilerServices;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Benchmark;

[HideColumns("Error", "RatioSD")]
public class NextStartBench
{
    public NextStartBench()
    {
        _fields = new uint[1024 * 8];

        for (int i = 0; i < _fields.Length; i++)
        {
            uint value = (uint)i;

            if (i % 4 == 0)
            {
                value |= Field.IsEOL;
            }

            if (i % 8 == 0)
            {
                value |= Field.IsCRLF;
            }

            _fields[i] = value;
        }

        _fields[0] = Field.StartOrEnd;
        _fields[^1] = Field.StartOrEnd;
    }

    private readonly uint[] _fields;

    [Benchmark(Baseline = true)]
    public uint Control()
    {
        uint f0 = 0;
        uint f1 = 0;
        uint f2 = 0;
        uint f3 = 0;
        uint f4 = 0;
        uint f5 = 0;
        uint f6 = 0;
        uint f7 = 0;

        for (int i = 0; i < (_fields.Length - 8); i += 8)
        {
            f0 += _fields[i];
            f1 += _fields[i + 1];
            f2 += _fields[i + 2];
            f3 += _fields[i + 3];
            f4 += _fields[i + 4];
            f5 += _fields[i + 5];
            f6 += _fields[i + 6];
            f7 += _fields[i + 7];
        }

        return f0 + f1 + f2 + f3 + f4 + f5 + f6 + f7;
    }

    // [Benchmark]
    public uint Bmi2()
    {
        uint f0 = 0;
        uint f1 = 0;
        uint f2 = 0;
        uint f3 = 0;
        uint f4 = 0;
        uint f5 = 0;
        uint f6 = 0;
        uint f7 = 0;

        for (int i = 0; i < (_fields.Length - 8); i += 8)
        {
            f0 += Bmi2Impl(_fields[i]);
            f1 += Bmi2Impl(_fields[i + 1]);
            f2 += Bmi2Impl(_fields[i + 2]);
            f3 += Bmi2Impl(_fields[i + 3]);
            f4 += Bmi2Impl(_fields[i + 4]);
            f5 += Bmi2Impl(_fields[i + 5]);
            f6 += Bmi2Impl(_fields[i + 6]);
            f7 += Bmi2Impl(_fields[i + 7]);
        }

        return f0 + f1 + f2 + f3 + f4 + f5 + f6 + f7;
    }

    [Benchmark]
    public uint BitShift()
    {
        uint f0 = 0;
        uint f1 = 0;
        uint f2 = 0;
        uint f3 = 0;
        uint f4 = 0;
        uint f5 = 0;
        uint f6 = 0;
        uint f7 = 0;

        for (int i = 0; i < (_fields.Length - 8); i += 8)
        {
            f0 += Shift(_fields[i]);
            f1 += Shift(_fields[i + 1]);
            f2 += Shift(_fields[i + 2]);
            f3 += Shift(_fields[i + 3]);
            f4 += Shift(_fields[i + 4]);
            f5 += Shift(_fields[i + 5]);
            f6 += Shift(_fields[i + 6]);
            f7 += Shift(_fields[i + 7]);
        }
        return f0 + f1 + f2 + f3 + f4 + f5 + f6 + f7;
    }

    // [Benchmark]
    public uint Lookup()
    {
        uint f0 = 0;
        uint f1 = 0;
        uint f2 = 0;
        uint f3 = 0;
        uint f4 = 0;
        uint f5 = 0;
        uint f6 = 0;
        uint f7 = 0;

        for (int i = 0; i < (_fields.Length - 8); i += 8)
        {
            f0 += LutImpl(_fields[i]);
            f1 += LutImpl(_fields[i + 1]);
            f2 += LutImpl(_fields[i + 2]);
            f3 += LutImpl(_fields[i + 3]);
            f4 += LutImpl(_fields[i + 4]);
            f5 += LutImpl(_fields[i + 5]);
            f6 += LutImpl(_fields[i + 6]);
            f7 += LutImpl(_fields[i + 7]);
        }
        return f0 + f1 + f2 + f3 + f4 + f5 + f6 + f7;
    }

    [Benchmark]
    public uint BitShift2()
    {
        uint f0 = 0;
        uint f1 = 0;
        uint f2 = 0;
        uint f3 = 0;
        uint f4 = 0;
        uint f5 = 0;
        uint f6 = 0;
        uint f7 = 0;

        for (int i = 0; i < (_fields.Length - 8); i += 8)
        {
            f0 += ShiftOpt(_fields[i]);
            f1 += ShiftOpt(_fields[i + 1]);
            f2 += ShiftOpt(_fields[i + 2]);
            f3 += ShiftOpt(_fields[i + 3]);
            f4 += ShiftOpt(_fields[i + 4]);
            f5 += ShiftOpt(_fields[i + 5]);
            f6 += ShiftOpt(_fields[i + 6]);
            f7 += ShiftOpt(_fields[i + 7]);
        }
        return f0 + f1 + f2 + f3 + f4 + f5 + f6 + f7;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static uint Bmi2Impl(uint field) => (uint)(0b_10_01_00_01 >> (int)((field >> 30) << 1)) & 3;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static uint Shift(uint field)
    {
        uint x = field >> 30;
        return (~x & 1) // result lsb should be empty unless input lsb is set
            | ((x & (x >> 1)) << 1); // result msb should only be set if both input bits are set
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static uint LutImpl(uint field) => Unsafe.Add(ref Unsafe.AsRef(in LutValue[0]), field >> 30);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static uint ShiftOpt(uint field)
    {
        return ((field >> 30) ^ 1u) + 1u;
    }

    private static ReadOnlySpan<byte> LutValue => [1, 0, 1, 2];
}
