using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.Diagnostics;
using FlameCsv.Readers;
using FlameCsv.Readers.Internal;
using CsvReader = CsvHelper.CsvReader;

namespace FlameCsv.Benchmark;

[SimpleJob]
[MemoryDiagnoser]
public class CsvEnumerateBench
{
    private static readonly byte[] _file = File.ReadAllBytes("/home/sipi/test.csv");

    [Benchmark(Baseline = true)]
    public async ValueTask CsvHelper()
    {
        await using var stream = new MemoryStream(_file);
        using var reader = new StreamReader(stream, Encoding.ASCII, false);
        var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
        {
            NewLine = Environment.NewLine,
            HasHeaderRecord = false,
        };

        using var csv = new CsvReader(reader, config);

        while (await csv.ReadAsync())
        {
            for (int i = 0; i < 14; i++)
            {
                _ = csv.GetField(i);
            }
        }
    }

    [Benchmark]
    public async ValueTask FlameCsv_ASCII()
    {
        await using var stream = new MemoryStream(_file);
        using var reader = new TextPipeReader(new StreamReader(stream, Encoding.ASCII, false), 4096);

        var options = CsvTokens<char>.Environment;
        using var _enumeratorBuffer = new BufferOwner<char>();

        while (true)
        {
            TextReadResult result = await reader.ReadAsync();
            ReadOnlySequence<char> buffer = result.Buffer;

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

            reader.AdvanceTo(buffer.Start, buffer.End);

            if (result.IsCompleted)
            {
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

                break;
            }
        }
    }

    [Benchmark]
    public async ValueTask FlameCsv_Utf8()
    {
        await using var stream = new MemoryStream(_file);
        var reader = PipeReader.Create(stream);

        var options = CsvTokens<byte>.Environment;
        using var _enumeratorBuffer = new BufferOwner<byte>();

        try
        {
            while (true)
            {
                ReadResult result = await reader.ReadAsync();
                ReadOnlySequence<byte> buffer = result.Buffer;

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

                reader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted)
                {
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

                    break;
                }
            }
        }
        finally
        {
            await reader.CompleteAsync();
        }
    }

    private static void EnumerateColumns<T>(
        in ReadOnlySequence<T> line,
        int stringDelimiterCount,
        in CsvTokens<T> _options,
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
        in CsvTokens<T> _options,
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
