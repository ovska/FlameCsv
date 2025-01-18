// ReSharper disable all

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;
using nietras.SeparatedValues;

namespace FlameCsv.Benchmark;

[MemoryDiagnoser(displayGenColumns: false)]
[HideColumns("Error", "StdDev")]
//[BenchmarkDotNet.Diagnostics.Windows.Configs.EtwProfiler]
public class CsvEnumerateBench
{
    private static readonly byte[] _bytes
        = File.ReadAllBytes("C:/Users/Sipi/source/repos/FlameCsv/FlameCsv.Tests/TestData/SampleCSVFile_556kb.csv");

    private static readonly string _chars = Encoding.ASCII.GetString(_bytes);
    private static MemoryStream GetFileStream() => new MemoryStream(_bytes);
    private static readonly ReadOnlySequence<byte> _byteSeq = new(_bytes.AsMemory());
    private static readonly ReadOnlySequence<char> _charSeq = new(_chars.AsMemory());

    //[Benchmark(Baseline = true)]
    //public void CsvHelper_Sync()
    //{
    //    using var reader = new StringReader(_chars);

    //    var config = new CsvHelper.Configuration.CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
    //    {
    //        NewLine = Environment.NewLine,
    //        HasHeaderRecord = false,
    //    };

    //    using var csv = new CsvHelper.CsvReader(reader, config);

    //    while (csv.Read())
    //    {
    //        for (int i = 0; i < 10; i++)
    //        {
    //            _ = csv.GetField(i);
    //        }
    //    }
    //}

    //[Benchmark]
    //public void Flame_Utf8()
    //{
    //    foreach (var record in new CsvRecordEnumerable<byte>(in _byteSeq, CsvOptions<byte>.Default))
    //    {
    //        foreach (var field in record)
    //        {
    //            _ = field;
    //        }
    //    }
    //}

    ////[Benchmark]
    ////public async ValueTask Flame_Utf8_Async()
    ////{
    ////    using var stream = GetFileStream();

    ////    await foreach (var record in CsvReader.EnumerateAsync(stream, CsvOptions<byte>.Default))
    ////    {
    ////        foreach (var field in record)
    ////        {
    ////            _ = field;
    ////        }
    ////    }
    ////}

    //[Benchmark]
    //public void Flame_Char()
    //{
    //    foreach (var record in new CsvRecordEnumerable<char>(in _charSeq, CsvOptions<char>.Default))
    //    {
    //        foreach (var field in record)
    //        {
    //            _ = field;
    //        }
    //    }
    //}

    //[Benchmark]
    //public async ValueTask Flame_Char_Async()
    //{
    //    await using var stream = GetFileStream();
    //    using var reader = new StreamReader(stream, Encoding.ASCII, false);

    //    await foreach (var record in CsvReader.EnumerateAsync(reader, CsvOptions<char>.Default))
    //    {
    //        foreach (var field in record)
    //        {
    //            _ = field;
    //        }
    //    }
    //}

    [Benchmark(Baseline = true)]
    public void FlameUTF2()
    {
        IMemoryOwner<byte>? allocated = null;
        Span<byte> unescapeBuffer = stackalloc byte[256];
        using var parser = CsvParser.Create(CsvOptions<byte>.Default);
        parser.Reset(new ReadOnlySequence<byte>(_bytes.AsMemory()));

        while (parser.TryReadLine(out var line, isFinalBlock: false))
        {
            foreach (var field in new MetaFieldReader<byte>(in line, unescapeBuffer))
            {
                _ = field;
            }
        }

        allocated?.Dispose();
    }

    // [Benchmark]
    public void Buffering()
    {
        Span<char> unescapeBuffer = stackalloc char[256];
        MetaV1[] array = ArrayPool<MetaV1>.Shared.Rent(1024);
        Span<MetaV1> metaBuffer = array;
        ReadOnlySpan<char> bytes = _chars;
        ref readonly CsvDialect<char> dialect = ref CsvOptions<char>.Default.Dialect;

        int count;

        while (true)
        {
            count = FieldReaderV1<char>.Read(bytes, metaBuffer, in dialect, _searcher, false);

            if (count == 0)
            {
                break;
            }

            for (int i = 0; i < count; i++)
            {
                var meta = metaBuffer[i];
                _ = meta.SliceUnsafe(in dialect, bytes, unescapeBuffer);
            }

            var last = metaBuffer[count - 1];
            bytes = bytes.Slice(last.GetStartOfNext(newlineLength: 2));
        }

        count = FieldReaderV1<char>.Read(bytes, metaBuffer, in dialect, _searcher, true);

        for (int i = 0; i < count; i++)
        {
            var meta = metaBuffer[i];
            _ = meta.SliceUnsafe(in dialect, bytes, unescapeBuffer);
        }

        ArrayPool<MetaV1>.Shared.Return(array);
    }

    private static readonly SearchValues<char> _searcher = SearchValues.Create(",\"\r\n");

    [Benchmark]
    public void Sepp()
    {
        var reader = nietras
            .SeparatedValues.Sep.Reader(
                o => o with
                {
                    Sep = new Sep(','),
                    CultureInfo = System.Globalization.CultureInfo.InvariantCulture,
                    HasHeader = false,
                })
            .From(_bytes);

        foreach (var row in reader)
        {
            for (int i = 0; i < row.ColCount; i++)
            {
                _ = row[i];
            }
        }
    }

    // [Benchmark]
    // public void Buffer2()
    // {
    //     Meta[] array = ArrayPool<Meta>.Shared.Rent(1024);
    //     Span<Meta> buffer = array;
    //     ReadOnlySpan<byte> bytes = _bytes;
    //
    //     int read;
    //     int runningIndex = 0;
    //
    //     while ((read = FieldParser<byte>.Read(
    //                bytes,
    //                buffer,
    //                in CsvOptions<byte>.Default.Dialect,
    //                NewlineBuffer<byte>.CRLF)) > 0)
    //     {
    //         for (int i = 0; i < read; i++)
    //         {
    //             // var slice = buffer[i];
    //             // var start = i == 0 ? runningIndex : runningIndex + buffer[i - 1].GetEndWithTrailing(2);
    //             // _ = bytes[start..(runningIndex + slice.End)];
    //             _ = buffer[i].GetEndWithTrailing(2);
    //         }
    //
    //         var last = buffer[read - 1];
    //         runningIndex += last.GetEndWithTrailing(2);
    //         bytes = bytes.Slice(last.GetEndWithTrailing(2));
    //     }
    //
    //     ArrayPool<Meta>.Shared.Return(array);
    // }
}

public readonly struct MetaV1
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MetaV1(int start, int length, uint quotesRemaining, bool isEOL)
    {
        Debug.Assert(start >= 0 && length >= 0);
        _start = isEOL ? start | int.MinValue : start;
        Length = length;
        QuotesRemaining = quotesRemaining;
    }

    private readonly int _start;

    public int Start => _start & ~int.MinValue;
    public int Length { get; }
    public uint QuotesRemaining { get; }
    public bool IsEOL => (_start & int.MinValue) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetStartOfNext(int newlineLength) => Start + Length + (IsEOL ? newlineLength : 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> SliceUnsafe<T>(
        ref readonly CsvDialect<T> dialect,
        ReadOnlySpan<T> data,
        Span<T> buffer)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (QuotesRemaining <= 2)
        {
            return MemoryMarshal.CreateReadOnlySpan(
                ref Unsafe.Add(ref MemoryMarshal.GetReference(data), Start + (QuotesRemaining != 0 ? 1 : 0)),
                unchecked(Length - (int)QuotesRemaining));
        }

        Debug.Assert(QuotesRemaining % 2 == 0);

        int unescapedLength = Length - (int)(QuotesRemaining / 2u) - 2;

        if (buffer.Length < unescapedLength)
        {
            Throw.Argument(nameof(buffer), "Not enough space to unescape");
        }

        RFC4180Mode<T>.Unescape(dialect.Quote, buffer, data.Slice(Start + 1, Length - 2), QuotesRemaining - 2);
        return buffer[..unescapedLength];
    }
}

file static class FieldReaderV1<T> where T : unmanaged, IBinaryInteger<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Read(
        scoped ReadOnlySpan<T> data,
        scoped Span<MetaV1> metaBufferSpan,
        ref readonly CsvDialect<T> dialect,
        SearchValues<T> anyToken,
        bool isFinalBlock)
    {
#if !DEBUG
        if (Unsafe.SizeOf<T>() == sizeof(char))
        {
            return FieldReaderV1<ushort>.ReadCore(
                MemoryMarshal.Cast<T, ushort>(data),
                metaBufferSpan,
                ref Unsafe.As<CsvDialect<T>, CsvDialect<ushort>>(ref Unsafe.AsRef(in dialect)),
                Unsafe.As<SearchValues<ushort>>(anyToken),
                isFinalBlock);
        }
#endif

        return ReadCore(data, metaBufferSpan, in dialect, anyToken, isFinalBlock);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static int ReadCore(
        scoped ReadOnlySpan<T> data,
        scoped Span<MetaV1> metaBufferSpan,
        ref readonly CsvDialect<T> dialect,
        SearchValues<T> anyToken,
        bool isFinalBlock)
    {
        int linesRead = 0;
        bool eol = false;
        uint quotesConsumed = 0;
        ReadOnlySpan<T> currentStart = data;
        ReadOnlySpan<T> remaining = data;
        var newline = NewlineBuffer<T>.CRLF;
        var searchValues = anyToken;

        scoped Span<MetaV1> metaBuffer = metaBufferSpan;

        while (linesRead < metaBuffer.Length)
        {
            int index = (quotesConsumed & 1) == 0
                ? remaining.IndexOfAny(searchValues)
                : remaining.IndexOf(dialect.Quote);

            if (index == -1)
            {
                break;
            }

            T token = remaining[index];

            if (token == dialect.Quote)
            {
                quotesConsumed++;
                remaining = remaining.Slice(index + 1);
                continue;
            }

            // cannot be inside a string in this case
            if (token == dialect.Delimiter)
            {
                goto FoundDelimiter;
            }

            if (token == newline.First &&
                (newline.Length == 1 || (remaining.Length >= index + 1 && remaining[index + 1] == newline.Second)))
            {
                goto FoundEOL;
            }

            remaining = remaining.Slice(index + 1);
            continue;

        FoundEOL:
            eol = true;
        FoundDelimiter:
            int start = GetOffset(data, currentStart);
            int length = currentStart.Length - remaining.Length + index;

            if (quotesConsumed != 0)
            {
                Debug.Assert(length >= 2);
                Debug.Assert(quotesConsumed % 2 == 0);

                if (MemoryMarshal.GetReference(currentStart) != dialect.Quote ||
                    Unsafe.Add(ref MemoryMarshal.GetReference(currentStart), length - 1) != dialect.Quote)
                {
                    Throw.InvalidOperation("Invalid state");
                }
            }

            metaBuffer[linesRead++] = new MetaV1(
                start: start,
                length: length,
                quotesRemaining: quotesConsumed,
                isEOL: eol);

            remaining = remaining.Slice(index + (eol ? newline.Length : 1));
            currentStart = remaining;
            quotesConsumed = 0;
            eol = false;
        }

        if (isFinalBlock && !currentStart.IsEmpty && linesRead < metaBuffer.Length)
        {
            metaBuffer[linesRead++] = new MetaV1(
                start: GetOffset(data, currentStart),
                length: currentStart.Length,
                quotesRemaining: quotesConsumed,
                isEOL: true);
        }

        return linesRead;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetOffset(ReadOnlySpan<T> data, ReadOnlySpan<T> remaining)
    {
        ref T dataStart = ref MemoryMarshal.GetReference(data);
        ref T dataEnd = ref MemoryMarshal.GetReference(remaining);
        nint offset = Unsafe.ByteOffset(ref dataStart, ref dataEnd);
        return (int)(offset / Unsafe.SizeOf<T>());
    }
}
