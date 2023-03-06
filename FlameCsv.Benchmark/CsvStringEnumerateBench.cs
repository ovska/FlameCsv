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
        var options = CsvTokens<char>.Environment;
        char[]? _buffer = null;

        while (LineReader.TryRead(in options, ref buffer, out var line, out int quoteCount))
        {
            if (line.IsSingleSegment)
            {
                EnumerateColumnSpan(line.FirstSpan, quoteCount, in options, ref _buffer);
            }
            else
            {
                EnumerateColumns(in line, quoteCount, in options, ref _buffer);
            }
        }

        // Read leftover data if there was no final newline
        if (!buffer.IsEmpty)
        {
            if (buffer.IsSingleSegment)
            {
                EnumerateColumnSpan(buffer.FirstSpan, 0, in options, ref _buffer);
            }
            else
            {
                EnumerateColumns(in buffer, 0, in options, ref _buffer);
            }
        }

        if (_buffer is not null) ArrayPool<char>.Shared.Return(_buffer);
    }

    // [Benchmark]
    // public void FlameCsv_Enumerator()
    // {
    //     var options = CsvUtf8ReaderOptions.Default;
    //     options.tokens = options.tokens.WithNewLine("\n");
    //
    //     var seq = new ReadOnlySequence<byte>(_bytes);
    //     foreach (var record in FlameCsv.Readers.CsvReader.Enumerate(seq, options))
    //     {
    //         foreach (var column in record)
    //         {
    //             _ = column;
    //         }
    //     }
    // }

    [Benchmark]
    public void FlameCsv_Utf8()
    {
        var buffer = new ReadOnlySequence<byte>(_bytes);
        var options = CsvTokens<byte>.Environment;
        byte[]? _buffer = null;

        while (LineReader.TryRead(in options, ref buffer, out var line, out int quoteCount))
        {
            if (line.IsSingleSegment)
            {
                EnumerateColumnSpan(line.FirstSpan, quoteCount, in options, ref _buffer);
            }
            else
            {
                EnumerateColumns(in line, quoteCount, in options, ref _buffer);
            }
        }

        // Read leftover data if there was no final newline
        if (!buffer.IsEmpty)
        {
            if (buffer.IsSingleSegment)
            {
                EnumerateColumnSpan(buffer.FirstSpan, 0, in options, ref _buffer);
            }
            else
            {
                EnumerateColumns(in buffer, 0, in options, ref _buffer);
            }
        }

        if (_buffer is not null) ArrayPool<byte>.Shared.Return(_buffer);
    }

    private static void EnumerateColumns<T>(
        in ReadOnlySequence<T> line,
        int stringDelimiterCount,
        in CsvTokens<T> _options,
        ref T[]? _buffer)
        where T : unmanaged, IEquatable<T>
    {
        Debug.Assert(!line.IsSingleSegment);

        int length = (int)line.Length;

        if (length <= 128)
        {
            Span<T> buffer = stackalloc T[length];
            line.CopyTo(buffer);
            EnumerateColumnSpan(buffer, stringDelimiterCount, in _options, ref _buffer);
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
        in CsvTokens<T> _options,
        ref T[]? _buffer)
        where T : unmanaged, IEquatable<T>
    {
        var enumerator = new CsvColumnEnumerator<T>(
            line,
            in _options,
            14,
            stringDelimiterCount,
            new ValueBufferOwner<T>(ref _buffer, ArrayPool<T>.Shared));

        while (enumerator.MoveNext())
            _ = enumerator.Current;
    }
}
