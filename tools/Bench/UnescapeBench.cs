using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using FlameCsv.IO;
using FlameCsv.Reading;
using FlameCsv.Reading.Unescaping;

namespace FlameCsv.Benchmark;

// | RFC    | 8.332 us | 0.0861 us | 0.0133 us |  1.00 |
// | RFC    | 8.318 us | 0.1316 us | 0.0469 us |  1.00 | remove unrolled
// | RFC    | 8.253 us | 0.0982 us | 0.0255 us |  1.00 | eager copy
/*

| Method | Mean     | Error     | StdDev    | Ratio |
|------- |---------:|----------:|----------:|------:|
| RFC    | 8.450 us | 0.1084 us | 0.0281 us |  1.00 |
| New    | 8.462 us | 0.0654 us | 0.0101 us |  1.00 |


| Method | Mean     | Error     | StdDev    | Ratio |
|------- |---------:|----------:|----------:|------:|
| RFC    | 8.574 us | 0.1453 us | 0.0518 us |  1.00 |
| New    | 8.534 us | 0.1382 us | 0.0723 us |  1.00 |

| Method | Mean     | Error     | StdDev    | Ratio |
|------- |---------:|----------:|----------:|------:|
| RFC    | 8.491 us | 0.1600 us | 0.0571 us |  1.00 |
| New    | 8.562 us | 0.1446 us | 0.0224 us |  1.01 |

|------- |---------:|----------:|----------:|------:|--------:|
| RFC    | 8.254 us | 0.1561 us | 0.0929 us |  1.00 |    0.02 |
| New    | 8.435 us | 0.1106 us | 0.0171 us |  1.02 |    0.01 |

| Method | Mean     | Error     | StdDev    | Ratio |
|------- |---------:|----------:|----------:|------:|
| RFC    | 8.279 us | 0.0874 us | 0.0135 us |  1.00 |
| New    | 8.489 us | 0.1656 us | 0.0256 us |  1.03 |

| Method | Mean     | Error     | StdDev    | Ratio |
|------- |---------:|----------:|----------:|------:|
| RFC    | 8.136 us | 0.1267 us | 0.0452 us |  1.00 |
| New    | 8.434 us | 0.0716 us | 0.0111 us |  1.04 |

| Method | Mean     | Error     | StdDev    | Ratio |
|------- |---------:|----------:|----------:|------:|
| RFC    | 7.993 us | 0.1543 us | 0.0550 us |  1.00 |
| New    | 8.241 us | 0.1323 us | 0.0205 us |  1.03 |

|------- |---------:|----------:|----------:|------:|
| RFC    | 7.759 us | 0.1399 us | 0.0621 us |  1.00 |
| New    | 8.239 us | 0.1230 us | 0.0438 us |  1.06 |

*/

public class UnescapeBench
{
    [Benchmark(Baseline = true)]
    public void RFC()
    {
        Span<ushort> buffer = stackalloc ushort[512];

        foreach (var f in _data)
        {
            RFC4180Mode<ushort>.Unescape(
                quote: '"',
                buffer: buffer,
                field: MemoryMarshal.Cast<char, ushort>(f.AsSpan()),
                quotesConsumed: 2
            );
        }
    }

    [Benchmark]
    public void New()
    {
        Span<ushort> buffer = stackalloc ushort[512];

        foreach (var f in _data)
        {
            RFC4180Mode<ushort>.UnescapeNew(
                quote: '"',
                buffer: buffer,
                field: MemoryMarshal.Cast<char, ushort>(f.AsSpan()),
                quotesConsumed: 2
            );
        }
    }

    private readonly string[] _data;
    private readonly byte[][] _bytes;

    public UnescapeBench()
    {
        using var reader = new CsvReader<char>(
            new() { Newline = CsvNewline.LF },
            CsvBufferReader.Create(File.OpenRead("Comparisons/Data/SampleCSVFile_556kb_4x.csv"), Encoding.UTF8)
        );

        List<string> data = [];

        foreach (var r in reader.ParseRecords())
        {
            for (uint i = 0; i < r.FieldCount; i++)
            {
                if (Unsafe.Add(ref r._quotes, i) > 2)
                {
                    data.Add(r.GetRawSpan((int)i)[1..^1].ToString());
                }
            }
        }

        _data = [.. data];
    }
}
