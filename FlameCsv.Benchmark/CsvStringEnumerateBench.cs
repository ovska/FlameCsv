using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.Diagnostics;
using FlameCsv.Readers;
using FlameCsv.Readers.Internal;
using CsvReader = CsvHelper.CsvReader;

namespace FlameCsv.Benchmark;

[SimpleJob]
[MemoryDiagnoser]
public class CsvStringEnumerateBench
{
    private static readonly byte[] _bytes = File.ReadAllBytes("/home/sipi/test.csv");
    private static readonly string _string = Encoding.UTF8.GetString(_bytes);

    [Benchmark(Baseline = true)]
    public void CsvHelper()
    {
        using var reader = new StringReader(_string);
        var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
        {
            NewLine = Environment.NewLine,
            HasHeaderRecord = false,
        };

        using var csv = new CsvReader(reader, config);

        while (csv.Read())
        {
            for (int i = 0; i < 14; i++)
            {
                _ = csv.GetField(i);
            }
        }
    }

    [Benchmark]
    public void FlameCsv_ASCII()
    {
        var buffer = new ReadOnlySequence<char>(_string.AsMemory());
        var options = CsvParserOptions<char>.Environment;
        using var _enumeratorBuffer = default(BufferOwner<char>)!;

        while (LineReader.TryRead(in options, ref buffer, out var line, out int quoteCount))
        {
            if (line.IsSingleSegment)
            {
                EnumerateColumnSpan(line.FirstSpan, quoteCount, in options, _enumeratorBuffer);
            }
            else
            {
                EnumerateColumns(in line, quoteCount, in options, _enumeratorBuffer);
            }
        }

        // Read leftover data if there was no final newline
        if (!buffer.IsEmpty)
        {
            if (buffer.IsSingleSegment)
            {
                EnumerateColumnSpan(buffer.FirstSpan, 0, in options, _enumeratorBuffer);
            }
            else
            {
                EnumerateColumns(in buffer, 0, in options, _enumeratorBuffer);
            }
        }
    }

    [Benchmark]
    public void FlameCsv_Utf8()
    {
        var buffer = new ReadOnlySequence<byte>(_bytes);
        var options = CsvParserOptions<byte>.Environment;
        using var _enumeratorBuffer = default(BufferOwner<byte>)!;

        while (LineReader.TryRead(in options, ref buffer, out var line, out int quoteCount))
        {
            if (line.IsSingleSegment)
            {
                EnumerateColumnSpan(line.FirstSpan, quoteCount, in options, _enumeratorBuffer);
            }
            else
            {
                EnumerateColumns(in line, quoteCount, in options, _enumeratorBuffer);
            }
        }

        // Read leftover data if there was no final newline
        if (!buffer.IsEmpty)
        {
            if (buffer.IsSingleSegment)
            {
                EnumerateColumnSpan(buffer.FirstSpan, 0, in options, _enumeratorBuffer);
            }
            else
            {
                EnumerateColumns(in buffer, 0, in options, _enumeratorBuffer);
            }
        }
    }

    private static void EnumerateColumns<T>(
        in ReadOnlySequence<T> line,
        int stringDelimiterCount,
        in CsvParserOptions<T> _options,
        BufferOwner<T> _enumeratorBuffer)
        where T : unmanaged, IEquatable<T>
    {
        Debug.Assert(!line.IsSingleSegment);

        int length = (int)line.Length;

        if (length <= 128)
        {
            Span<T> buffer = stackalloc T[length];
            line.CopyTo(buffer);
            EnumerateColumnSpan(buffer, stringDelimiterCount, in _options, _enumeratorBuffer);
        }
        else
        {
            ThrowHelper.ThrowNotSupportedException();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EnumerateColumnSpan<T>(
        ReadOnlySpan<T> line,
        int stringDelimiterCount,
        in CsvParserOptions<T> _options,
        BufferOwner<T> _enumeratorBuffer)
        where T : unmanaged, IEquatable<T>
    {
        var enumerator = new CsvColumnEnumerator<T>(
            line,
            in _options,
            14,
            stringDelimiterCount,
            _enumeratorBuffer);

        foreach (var column in enumerator)
        {
            _ = column;
        }
    }
}
