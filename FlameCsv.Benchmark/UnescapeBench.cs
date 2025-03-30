using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;
using FlameCsv.Reading.Unescaping;

namespace FlameCsv.Benchmark;

public class UnescapeBench
{
    private record struct Field(int Start, int Length, uint QuoteCount);

    private static readonly byte[] _bytes = File.ReadAllBytes("Comparisons/Data/SampleCSVFile_556kb.csv");
    private static readonly string _chars = File.ReadAllText("Comparisons/Data/SampleCSVFile_556kb.csv");

    private readonly Field[] _byteFields = ReadFields<byte>(_bytes);
    private readonly Field[] _charFields = ReadFields(_chars.AsMemory());

    private readonly byte[] _byteBuffer = new byte[1024];
    private readonly char[] _charBuffer = new char[1024];

    [Params(true, false)] public bool Bytes { get; set; }

    // [Benchmark(Baseline = true)]
    // public void IndexOf()
    // {
    //     if (Bytes)
    //     {
    //         foreach ((int start, int length, uint quoteCount) in _byteFields)
    //         {
    //             var unescaper = new IndexOfRFC4180Unescaper<byte>((byte)'"', quoteCount);
    //             var len = IndexOfRFC4180Unescaper<byte>.UnescapedLength(length, quoteCount);
    //             var field = new ReadOnlySpan<byte>(_bytes, start, length);
    //             IndexOfUnescaper.Field(field, unescaper, _byteBuffer.AsSpan(0, len));
    //         }
    //     }
    //     else
    //     {
    //         foreach ((int start, int length, uint quoteCount) in _charFields)
    //         {
    //             var unescaper = new IndexOfRFC4180Unescaper<char>('"', quoteCount);
    //             var len = IndexOfRFC4180Unescaper<char>.UnescapedLength(length, quoteCount);
    //             var field = _chars.AsSpan(start, length);
    //             IndexOfUnescaper.Field(field, unescaper, _charBuffer.AsSpan(0, len));
    //         }
    //     }
    // }
    //
    // [Benchmark]
    // public void Simd()
    // {
    //     if (Bytes)
    //     {
    //         foreach ((int start, int length, _) in _byteFields)
    //         {
    //             var field = new ReadOnlySpan<byte>(_bytes, start, length);
    //             _ = Unescaper.Unescape<byte, uint, Vector256<byte>, ByteAvx2Unescaper>(
    //                 (byte)'"',
    //                 field,
    //                 _byteBuffer);
    //         }
    //     }
    //     else
    //     {
    //         foreach ((int start, int length, _) in _charFields)
    //         {
    //             var field = _chars.AsSpan(start, length);
    //
    //
    //             _ = Unescaper.Unescape<char, ushort, Vector256<short>, CharAvxUnescaper>(
    //                 '"',
    //                 field,
    //                 _charBuffer);
    //         }
    //     }
    // }

    [Benchmark]
    public void Unrolled_Simd()
    {
        if (Bytes)
        {
            foreach ((int start, int length, uint quoteCount) in _byteFields)
            {
                var len = IndexOfRFC4180Unescaper<byte>.UnescapedLength(length, quoteCount);
                var field = new ReadOnlySpan<byte>(_bytes, start, length);
                RFC4180Mode<byte>.Unescape<Vec256Byte>((byte)'"', _byteBuffer.AsSpan(0, len), field, quoteCount);
            }
        }
        else
        {
            foreach ((int start, int length, uint quoteCount) in _charFields)
            {
                var len = IndexOfRFC4180Unescaper<char>.UnescapedLength(length, quoteCount);
                var field = _chars.AsSpan(start, length);
                RFC4180Mode<char>.Unescape<Vec128Char>('"', _charBuffer.AsSpan(0, len), field, quoteCount);
            }
        }
    }

    // [Benchmark]
    // public void Unrolled_Scalar()
    // {
    //     if (Bytes)
    //     {
    //         foreach ((int start, int length, uint quoteCount) in _byteFields)
    //         {
    //             var len = IndexOfRFC4180Unescaper<byte>.UnescapedLength(length, quoteCount);
    //             var field = new ReadOnlySpan<byte>(_bytes, start, length);
    //             RFC4180Mode<byte>.Unescape<NoOpVector<byte>>((byte)'"', _byteBuffer.AsSpan(0, len), field, quoteCount);
    //         }
    //     }
    //     else
    //     {
    //         foreach ((int start, int length, uint quoteCount) in _charFields)
    //         {
    //             var len = IndexOfRFC4180Unescaper<char>.UnescapedLength(length, quoteCount);
    //             var field = _chars.AsSpan(start, length);
    //             RFC4180Mode<char>.Unescape<NoOpVector<char>>('"', _charBuffer.AsSpan(0, len), field, quoteCount);
    //         }
    //     }
    // }


    private static Field[] ReadFields<T>(ReadOnlyMemory<T> data) where T : unmanaged, IBinaryInteger<T>
    {
        List<Field> byteFields = [];

        using var byteParser = CsvParser.Create(CsvOptions<T>.Default, new ReadOnlySequence<T>(data));
        ref T startOfData = ref MemoryMarshal.GetReference(data.Span);

        byteParser.TryAdvanceReader();

        while (byteParser.TryReadLine(out CsvFields<T> fields, false))
        {
            for (int i = 0; i < fields.FieldCount; i++)
            {
                if (fields.Fields[i + 1].SpecialCount <= 2) continue;

                var field = fields.GetField(i, raw: true)[1..^1];

                if (field.Length < 32) continue;

                var start = Unsafe.ByteOffset(ref startOfData, ref MemoryMarshal.GetReference(field)) /
                    Unsafe.SizeOf<T>();
                var length = field.Length;
                byteFields.Add(new(checked((int)start), length, (uint)field.Count(T.CreateTruncating('"'))));
            }
        }

        return byteFields.ToArray();
    }
}
