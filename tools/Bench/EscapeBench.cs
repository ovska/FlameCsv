using System.Text;
using FlameCsv.Writing.Escaping;

namespace FlameCsv.Benchmark;

/*
| Method       | Mean     | Error     | StdDev    |
|------------- |---------:|----------:|----------:|
| _Escape_Char | 9.848 us | 0.0519 us | 0.0231 us |
| _Escape_Byte | 8.214 us | 0.1609 us | 0.0958 us |
*/

public class EscapeBench
{
    private static readonly char[] _charBuffer = new char[8096];
    private static readonly byte[] _byteBuffer = new byte[8096];

    [Benchmark]
    public void _Escape_Char()
    {
        Span<char> buffer = _charBuffer;

        foreach (var (value, count) in _fieldsChar)
        {
            Escape.Scalar('"', value.AsSpan(), buffer, count);
        }
    }

    [Benchmark]
    public void _Escape_Byte()
    {
        Span<byte> buffer = _byteBuffer;
        foreach (var (value, count) in _fieldsByte)
        {
            Escape.Scalar((byte)'"', value, buffer, count);
        }
    }

    private readonly (string, int)[] _fieldsChar;
    private readonly (byte[], int)[] _fieldsByte;

    public EscapeBench()
    {
        List<(string, int)> fieldsChar = [];
        List<(byte[], int)> fieldsByte = [];

        foreach (var r in Csv.FromFile("Comparisons/Data/SampleCSVFile_556kb_4x.csv").ToReader())
        {
            for (int i = 0; i < r.FieldCount; i++)
            {
                if (r.GetMetadata(i).NeedsUnescaping)
                {
                    string value = r[i].ToString();
                    int count = value.Count('"');
                    fieldsChar.Add((value, count));
                    fieldsByte.Add((Encoding.UTF8.GetBytes(value), count));
                }
            }
        }

        _fieldsChar = [.. fieldsChar];
        _fieldsByte = [.. fieldsByte];
    }
}
