#if false
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using FlameCsv.IO;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;

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
    [Params(false, true)]
    public bool Chars { get; set; }

    [Benchmark]
    public void NoCount()
    {
        if (Chars)
        {
            Span<ushort> buffer = stackalloc ushort[512];

            foreach (var (f, c) in _data)
            {
                Field.Unescape(quote: '"', destination: buffer, source: MemoryMarshal.Cast<char, ushort>(f.AsSpan()));
            }
        }
        else
        {
            Span<byte> buffer = stackalloc byte[512];

            foreach (var (f, c) in _dataBytes)
            {
                Field.Unescape(quote: (byte)'"', destination: buffer, source: f);
            }
        }
    }

    private readonly (string, int)[] _data;
    private readonly (byte[], int)[] _dataBytes;

    public UnescapeBench()
    {
        using (
            var readerChar = new CsvReader<char>(
                new() { Newline = CsvNewline.LF },
                CsvBufferReader.Create(File.OpenRead("Comparisons/Data/SampleCSVFile_556kb_4x.csv"), Encoding.UTF8)
            )
        )
        {
            List<(string, int)> data = [];

            foreach (var r in readerChar.ParseRecords())
            {
                for (int i = 0; i < r.FieldCount; i++)
                {
                    if (((r._bits[i] >> 62) & 1) != 0)
                    {
                        var value = r.GetRawSpan(i)[1..^1].ToString();
                        data.Add((value, value.Count('"')));
                    }
                }
            }
            _data = [.. data];
        }

        List<(byte[], int)> dataBytes = [];
        using var readerByte = new CsvReader<byte>(
            new() { Newline = CsvNewline.LF },
            CsvBufferReader.Create(File.OpenRead("Comparisons/Data/SampleCSVFile_556kb_4x.csv"))
        );

        foreach (var r in readerByte.ParseRecords())
        {
            for (int i = 0; i < r.FieldCount; i++)
            {
                if (((r._bits[i] >> 62) & 1) != 0)
                {
                    byte[] value = r.GetRawSpan(i)[1..^1].ToArray();
                    dataBytes.Add((value, value.Count((byte)'"')));
                }
            }
        }

        _dataBytes = [.. dataBytes];
    }
}
#endif
