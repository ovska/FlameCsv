using System.Buffers;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using FlameCsv.Reading;
using FlameCsv.Utilities;

namespace FlameCsv.Benchmark;

public static class TestData
{
    private static readonly CsvOptions<byte> _optionsUtf8 = new() { Newline = "\r\n" };
    private static readonly CsvOptions<char> _optionsText = new() { Newline = "\r\n" };

    public static readonly Instance SampleCsvFile556Kb = new("SampleCSVFile_556kb.csv");
    public static readonly Instance Customers10000 = new("customers-10000.zip");

    public sealed class Instance
    {
        public byte[] Utf8
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _utf8.Value;
        }

        public char[] Text
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _text.Value;
        }

        public byte[][] LinesUtf8
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _linesUtf8.Value;
        }

        public char[][] LinesText
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _linesText.Value;
        }

        public byte[][] FieldsUtf8
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _fieldsUtf8.Value;
        }

        public char[][] FieldsText
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _fieldsText.Value;
        }

        public Instance(string path)
        {
            _utf8 = new(
                () =>
                {
                    if (!Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        return File.ReadAllBytes(path);
                    }

                    using var stream = new FileStream(
                        path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        Environment.SystemPageSize,
                        OperatingSystem.IsWindows() ? FileOptions.SequentialScan : FileOptions.None);
                    using var gzip = new GZipStream(stream, CompressionMode.Decompress);
                    using var ms = new MemoryStream();
                    gzip.CopyTo(ms);
                    return ms.ToArray();
                });

            _text = new(
                () =>
                {
                    var bytes = _utf8.Value;
                    var chars = new char[Encoding.UTF8.GetCharCount(bytes)];

                    if (Encoding.UTF8.GetChars(bytes, chars) != chars.Length)
                        throw new InvalidOperationException("Invalid UTF-8 data.");

                    return chars;
                });

            _linesUtf8 = new(
                () =>
                {
                    using var lines = new ValueListBuilder<byte[]>(capacity: 1024);

                    foreach (var line in EnumerateLines(_utf8.Value, _optionsUtf8))
                    {
                        lines.Append(line.Record.ToArray());
                    }

                    return lines.AsSpan().ToArray();
                });

            _linesText = new(
                () =>
                {
                    using var lines = new ValueListBuilder<char[]>(capacity: 1024);

                    foreach (var line in EnumerateLines(_text.Value, _optionsText))
                    {
                        lines.Append(line.Record.ToArray());
                    }

                    return lines.AsSpan().ToArray();
                });

            _fieldsUtf8 = new(
                () =>
                {
                    using var lines = new ValueListBuilder<byte[]>(capacity: 1024);

                    foreach (var line in EnumerateLines(_utf8.Value, _optionsUtf8))
                    {
                        var reader = new RawFieldReader<byte>(in line);

                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            lines.Append(reader[i].ToArray());
                        }
                    }

                    return lines.AsSpan().ToArray();
                });

            _fieldsText = new(
                () =>
                {
                    using var lines = new ValueListBuilder<char[]>(capacity: 1024);

                    foreach (var line in EnumerateLines(_text.Value, _optionsText))
                    {
                        var reader = new RawFieldReader<char>(in line);

                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            lines.Append(reader[i].ToArray());
                        }
                    }

                    return lines.AsSpan().ToArray();
                });
        }

        private readonly Lazy<byte[]> _utf8;
        private readonly Lazy<char[]> _text;
        private readonly Lazy<byte[][]> _linesUtf8;
        private readonly Lazy<char[][]> _linesText;
        private readonly Lazy<byte[][]> _fieldsUtf8;
        private readonly Lazy<char[][]> _fieldsText;
    }

    private static IEnumerable<CsvLine<T>> EnumerateLines<T>(T[] value, CsvOptions<T> options)
        where T : unmanaged, IBinaryInteger<T>
    {
        using var parser = CsvParser.Create(options);
        parser.Reset(new ReadOnlySequence<T>(value));

        while (parser.TryReadLine(out var line, isFinalBlock: false))
        {
            yield return line;
        }

        if (parser.TryReadLine(out var lastLine, isFinalBlock: true))
        {
            yield return lastLine;
        }
    }
}
