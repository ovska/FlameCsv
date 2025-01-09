using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using FlameCsv.Reading;

namespace FlameCsv.Benchmark;

public class CountEscapableBench
{
    private char[][] _fields = [];

    [GlobalSetup]
    public void Setup()
    {
        List<char[]> fields = [];

        var data = File.ReadLines(
            "C:/Users/Sipi/source/repos/FlameCsv/FlameCsv.Tests/TestData/SampleCSVFile_556kb.csv",
            Encoding.ASCII);

        IMemoryOwner<char>? buffer = null;
        char[] unescapeBuffer = new char[1024];

        using var parser = CsvParser<char>.Create(CsvOptions<char>.Default);

        foreach (var line in data)
        {
            var meta = parser.GetAsCsvLine(line);
            var reader = new CsvFieldReader<char>(CsvOptions<char>.Default, line, unescapeBuffer, ref buffer, in meta);

            while (reader.MoveNext())
            {
                fields.Add(reader.Current.ToArray());
            }
        }

        buffer?.Dispose();

        _fields = fields.ToArray();
    }

    [Benchmark(Baseline = true)]
    public void MemoryExt()
    {
        foreach (var field in _fields)
        {
            _ = CountEscapable1(field, '"', '\\');
        }
    }

    [Benchmark]
    public void ManualLoop()
    {
        foreach (var field in _fields)
        {
            _ = CountEscapable2(field, '"', '\\');
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int CountEscapable1<T>(scoped ReadOnlySpan<T> field, T quote, T escape)
        where T : unmanaged, IBinaryInteger<T>{
        return field.Count(quote) + field.Count(escape);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountEscapable2<T>(scoped ReadOnlySpan<T> field, T quote, T escape)
        where T : unmanaged, IBinaryInteger<T>
    {
        ref T r0 = ref MemoryMarshal.GetReference(field);
        nint rem = field.Length - 1;

        nint c0 = 0;
        nint c1 = 0;
        nint c2 = 0;
        nint c3 = 0;

        while (rem >= 4)
        {
            c0 += (Unsafe.Add(ref r0, rem - 0).Equals(quote) || Unsafe.Add(ref r0, rem - 0).Equals(escape)) ? 1 : 0;
            c1 += (Unsafe.Add(ref r0, rem - 1).Equals(quote) || Unsafe.Add(ref r0, rem - 1).Equals(escape)) ? 1 : 0;
            c2 += (Unsafe.Add(ref r0, rem - 2).Equals(quote) || Unsafe.Add(ref r0, rem - 2).Equals(escape)) ? 1 : 0;
            c3 += (Unsafe.Add(ref r0, rem - 3).Equals(quote) || Unsafe.Add(ref r0, rem - 3).Equals(escape)) ? 1 : 0;
            rem -= 4;
        }

        while (rem >= 0)
        {
            c0 += (Unsafe.Add(ref r0, rem).Equals(quote) || Unsafe.Add(ref r0, rem).Equals(escape)) ? 1 : 0;
            rem--;
        }

        return (int)(c0 + c1 + c2 + c3);
    }
}
