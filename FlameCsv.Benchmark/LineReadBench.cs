// using System.Buffers;
// using FlameCsv.Readers;
//
// namespace FlameCsv.Benchmark;
//
// #nullable disable
//
// [SimpleJob]
// public class LineReadBench
// {
//     [Params(true, false)] public bool CRLF { get; set; }
//     [Params(true, false)] public bool Strings { get; set; }
//     [Params(true, false)] public bool AddNL { get; set; }
//     [Params(32, 64, 256)] public int MinLen { get; set; }
//     private char[] _data;
//     private CsvParserOptions<char> _options;
//
//     [Benchmark(Baseline = false)]
//     public void WithFast()
//     {
//         var _sequence = new ReadOnlySequence<char>(_data);
//
//         while (LineReader<char>.TryReadLine(in _options, ref _sequence, out _, out _))
//         {
//         }
//     }
//
//     [Benchmark(Baseline = true)]
//     public void IndexOf()
//     {
//         var _sequence = new ReadOnlySequence<char>(_data);
//
//         while (LineReader<char>.TryReadLine3(in _options, ref _sequence, out _, out _))
//         {
//         }
//     }
//
//     [GlobalSetup]
//     public void Setup()
//     {
//         var newline = CRLF ? "\r\n" : "\n";
//         _options = CsvParserOptionDefaults.Environment<char>() with { NewLine = newline.ToCharArray() };
//         var data = Enumerable.Range(0, 1024)
//             .Select(i => new string('x', MinLen) + i)
//             .Select(s => Strings ? $"\"{s + (AddNL ? newline : "")}\"" : s.ToString())
//             .ToArray();
//         _data = string.Join(newline, data).ToCharArray();
//     }
// }
