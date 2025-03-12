#if false
using System.Diagnostics;
using System.Runtime.CompilerServices;
using FlameCsv.Writing;

namespace FlameCsv.Benchmark;

// [SimpleJob]
public class EscapeBench
{
    private readonly char[] _buffer = new char[1024];

    private RFC4180Escaper<char> _escaper;
    private (string line, int specialCount)[] _input = [];

    [GlobalSetup]
    public void Setup()
    {
        throw new NotImplementedException();

        // List<(string line, int specialCount)> fields = [];
        //
        // var data = File.ReadLines(
        //     "C:/Users/Sipi/source/repos/FlameCsv/FlameCsv.Tests/TestData/SampleCSVFile_556kb.csv",
        //     Encoding.ASCII);
        //
        // _escaper = new RFC4180Escaper<char>(',');
        // IMemoryOwner<char>? buffer = null;
        // char[] unescapeBuffer = new char[1024];
        //
        // using var parser = CsvParser<char>.Create(CsvOptions<char>.Default);
        //
        // foreach (var line in data)
        // {
        //     var meta = parser.GetAsCsvLine(line.AsMemory());
        //     var reader = new CsvFieldReader<char>(CsvOptions<char>.Default, in meta, unescapeBuffer, ref buffer);
        //
        //     while (reader.MoveNext())
        //     {
        //         if (reader.Current.ContainsAny(CsvOptions<char>.Default.Dialect.NeedsQuoting))
        //         {
        //             fields.Add((reader.Current.ToString(), _escaper.CountEscapable(reader.Current)));
        //         }
        //     }
        // }
        //
        // buffer?.Dispose();
        //
        // _input = fields.ToArray();
    }

    // [Benchmark(Baseline = true)]
    // public void Old()
    // {
    //     foreach ((string input, int specialCount) in _input)
    //     {
    //         Escape2.Field(ref _escaper, input, _buffer.AsSpan(0, input.Length + 2 + specialCount), specialCount);
    //     }
    // }

    [Benchmark(Baseline = false)]
    public void New()
    {
        foreach ((string input, int specialCount) in _input)
        {
            Escape.Field(ref _escaper, input, _buffer.AsSpan(0, input.Length + 2 + specialCount), specialCount);
        }
    }
}

file static class Escape2
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Field<T, TEscaper>(
        ref TEscaper escaper,
        scoped ReadOnlySpan<T> source,
        scoped Span<T> destination,
        int specialCount)
        where T : unmanaged, IBinaryInteger<T>
        where TEscaper : IEscaper<T>
    {
        Debug.Assert(destination.Length >= source.Length + specialCount + 2, "Destination buffer is too small");
        Debug.Assert(
            !source.Overlaps(destination, out int elementOffset) || elementOffset == 0,
            "If src and dst overlap, they must have the same starting point in memory");

        // Work backwards as the source and destination buffers might overlap
        int dstIndex = destination.Length - 1;
        int srcIndex = source.Length - 1;

        destination[dstIndex--] = escaper.Quote;

        while (specialCount > 0)
        {
            bool needsEscaping = escaper.NeedsEscaping(source[srcIndex]);

            destination[dstIndex--] = source[srcIndex--];

            if (needsEscaping)
            {
                destination[dstIndex--] = escaper.Escape;
                specialCount--;
            }
        }

        source.Slice(0, srcIndex + 1).CopyTo(destination.Slice(1));
        destination[0] = escaper.Quote;
    }
}
#endif
