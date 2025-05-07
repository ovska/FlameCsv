#if SIMD_UNESCAPING
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using FlameCsv.Intrinsics;
using FlameCsv.Reading;
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

    /// <summary>
    /// Naive IndexOf-based unescaping.
    /// </summary>
    [Benchmark(Baseline = true)]
    public void IndexOf()
    {
        if (Bytes)
        {
            foreach ((int start, int length, uint quoteCount) in _byteFields)
            {
                var unescaper = new IndexOfRFC4180Unescaper<byte>((byte)'"', quoteCount);
                var len = IndexOfRFC4180Unescaper<byte>.UnescapedLength(length, quoteCount);
                var field = new ReadOnlySpan<byte>(_bytes, start, length);
                IndexOfUnescaper.Field(field, unescaper, _byteBuffer.AsSpan(0, len));
            }
        }
        else
        {
            foreach ((int start, int length, uint quoteCount) in _charFields)
            {
                var unescaper = new IndexOfRFC4180Unescaper<char>('"', quoteCount);
                var len = IndexOfRFC4180Unescaper<char>.UnescapedLength(length, quoteCount);
                var field = _chars.AsSpan(start, length);
                IndexOfUnescaper.Field(field, unescaper, _charBuffer.AsSpan(0, len));
            }
        }
    }

    /// <summary>
    /// Loads vectors and compresses them where a quote is found.
    /// </summary>
    /// <remarks>
    /// Requires fields to be aligned to be at least 32 bytes.
    /// </remarks>
    // [Benchmark]
    public void Compressing()
    {
        if (Bytes)
        {
            foreach ((int start, int length, _) in _byteFields)
            {
                var field = new ReadOnlySpan<byte>(_bytes, start, length);
                _ = Unescaper.Unescape<byte, uint, Vector256<byte>, ByteAvx2Unescaper>(
                    (byte)'"',
                    field,
                    _byteBuffer);
            }
        }
        else
        {
            foreach ((int start, int length, _) in _charFields)
            {
                var field = _chars.AsSpan(start, length);


                _ = Unescaper.Unescape<char, ushort, Vector256<short>, CharAvxUnescaper>(
                    '"',
                    field,
                    _charBuffer);
            }
        }
    }

    /// <summary>
    /// Finds index of next quote using variable length vectors.
    /// </summary>
    [Benchmark]
    public void HybridVariable()
    {
        if (Bytes)
        {
            foreach ((int start, int length, uint quoteCount) in _byteFields)
            {
                var len = IndexOfRFC4180Unescaper<byte>.UnescapedLength(length, quoteCount);
                var field = new ReadOnlySpan<byte>(_bytes, start, length);

                if (field.Length >= Vec512Byte.Count * 2)
                {
                    OldUnescaper<byte>.Unescape<Vec512Byte>((byte)'"', _byteBuffer.AsSpan(0, len), field, quoteCount);
                }
                else if (field.Length >= Vec256Byte.Count * 2)
                {
                    OldUnescaper<byte>.Unescape<Vec256Byte>((byte)'"', _byteBuffer.AsSpan(0, len), field, quoteCount);
                }
                else if (field.Length >= Vec128Byte.Count * 2)
                {
                    OldUnescaper<byte>.Unescape<Vec128Byte>((byte)'"', _byteBuffer.AsSpan(0, len), field, quoteCount);
                }
                else
                {
                    OldUnescaper<byte>.Unescape<NoOpVector<byte>>(
                        (byte)'"',
                        _byteBuffer.AsSpan(0, len),
                        field,
                        quoteCount);
                }
            }
        }
        else
        {
            foreach ((int start, int length, uint quoteCount) in _charFields)
            {
                var len = IndexOfRFC4180Unescaper<char>.UnescapedLength(length, quoteCount);
                var field = _chars.AsSpan(start, length);

                if (field.Length >= Vec512Char.Count * 2)
                {
                    OldUnescaper<char>.Unescape<Vec512Char>('"', _charBuffer.AsSpan(0, len), field, quoteCount);
                }
                else if (field.Length >= Vec256Char.Count * 2)
                {
                    OldUnescaper<char>.Unescape<Vec256Char>('"', _charBuffer.AsSpan(0, len), field, quoteCount);
                }
                else if (field.Length >= Vec128Char.Count * 2)
                {
                    OldUnescaper<char>.Unescape<Vec128Char>('"', _charBuffer.AsSpan(0, len), field, quoteCount);
                }
                else
                {
                    OldUnescaper<char>.Unescape<NoOpVector<char>>('"', _charBuffer.AsSpan(0, len), field, quoteCount);
                }
            }
        }
    }

    /// <summary>
    /// Uses unrolled loops.
    /// </summary>
    [Benchmark]
    public void Scalar()
    {
        if (Bytes)
        {
            foreach ((int start, int length, uint quoteCount) in _byteFields)
            {
                var len = IndexOfRFC4180Unescaper<byte>.UnescapedLength(length, quoteCount);
                var field = new ReadOnlySpan<byte>(_bytes, start, length);
                OldUnescaper<byte>.Unescape<NoOpVector<byte>>((byte)'"', _byteBuffer.AsSpan(0, len), field, quoteCount);
            }
        }
        else
        {
            foreach ((int start, int length, uint quoteCount) in _charFields)
            {
                var len = IndexOfRFC4180Unescaper<char>.UnescapedLength(length, quoteCount);
                var field = _chars.AsSpan(start, length);
                OldUnescaper<char>.Unescape<NoOpVector<char>>('"', _charBuffer.AsSpan(0, len), field, quoteCount);
            }
        }
    }

    /// <summary>
    /// Uses unrolled loops, or on large inputs, finds index using a 256bit vector.
    /// </summary>
    [Benchmark]
    public void Hybrid256()
    {
        if (Bytes)
        {
            foreach ((int start, int length, uint quoteCount) in _byteFields)
            {
                var len = IndexOfRFC4180Unescaper<byte>.UnescapedLength(length, quoteCount);
                var field = new ReadOnlySpan<byte>(_bytes, start, length);
                RFC4180Mode<byte>.Unescape((byte)'"', _byteBuffer.AsSpan(0, len), field, quoteCount);
            }
        }
        else
        {
            foreach ((int start, int length, uint quoteCount) in _charFields)
            {
                var len = IndexOfRFC4180Unescaper<char>.UnescapedLength(length, quoteCount);
                var field = _chars.AsSpan(start, length);
                RFC4180Mode<ushort>.Unescape(
                    '"',
                    MemoryMarshal.Cast<char, ushort>(_charBuffer.AsSpan(0, len)),
                    MemoryMarshal.Cast<char, ushort>(field),
                    quoteCount);
            }
        }
    }

    private static Field[] ReadFields<T>(ReadOnlyMemory<T> data) where T : unmanaged, IBinaryInteger<T>
    {
        List<Field> byteFields = [];

        using var byteParser = new CsvReader<T>(CsvOptions<T>.Default, new ReadOnlySequence<T>(data));
        ref T startOfData = ref MemoryMarshal.GetReference(data.Span);

        byteParser.TryAdvanceReader();

        while (byteParser.TryReadLine(out CsvFields<T> fields))
        {
            for (int i = 0; i < fields.FieldCount; i++)
            {
                if (fields.Fields[i + 1].SpecialCount <= 2) continue;

                var field = fields.GetField(i, raw: true)[1..^1];

                // if (field.Length < 32) continue;

                var start = Unsafe.ByteOffset(ref startOfData, ref MemoryMarshal.GetReference(field)) /
                    Unsafe.SizeOf<T>();
                var length = field.Length;
                byteFields.Add(new(checked((int)start), length, (uint)field.Count(T.CreateTruncating('"'))));
            }
        }

        return byteFields.ToArray();
    }

    private static class OldUnescaper<T> where T : unmanaged, IBinaryInteger<T>
    {
            [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Unescape<TVector>(
        T quote,
        scoped Span<T> buffer,
        ReadOnlySpan<T> field,
        uint quotesConsumed)
        where TVector : struct, ISimdVector<T, TVector>
    {
        int quotesLeft = (int)quotesConsumed;

        nint srcIndex = 0;
        nint dstIndex = 0;
        nint srcLength = field.Length;

        TVector quoteVector = TVector.Create(quote);

        // leave 1 space for the second quote
        nint searchSpaceEnd = field.Length - 1;
        nint unrolledEnd = field.Length - 8 - 1;
        nint vectorizedEnd = field.Length - TVector.Count - 1;

        ref T src = ref MemoryMarshal.GetReference(field);
        ref T dst = ref MemoryMarshal.GetReference(buffer);

        goto ContinueRead;

    Found1:
        Unsafe.Add(ref dst, dstIndex) = Unsafe.Add(ref src, srcIndex);
        srcIndex += 1;
        dstIndex += 1;
        goto FoundLong;
    Found2:
        Unsafe.Add(ref dst, dstIndex) = Unsafe.Add(ref src, srcIndex);
        Unsafe.Add(ref dst, dstIndex + 1) = Unsafe.Add(ref src, srcIndex + 1);
        srcIndex += 2;
        dstIndex += 2;
        goto FoundLong;
    Found3:
        Unsafe.Add(ref dst, dstIndex) = Unsafe.Add(ref src, srcIndex);
        Unsafe.Add(ref dst, dstIndex + 1) = Unsafe.Add(ref src, srcIndex + 1);
        Unsafe.Add(ref dst, dstIndex + 2) = Unsafe.Add(ref src, srcIndex + 2);
        srcIndex += 3;
        dstIndex += 3;
        goto FoundLong;
    Found4:
        Unsafe.Add(ref dst, dstIndex) = Unsafe.Add(ref src, srcIndex);
        Unsafe.Add(ref dst, dstIndex + 1) = Unsafe.Add(ref src, srcIndex + 1);
        Unsafe.Add(ref dst, dstIndex + 2) = Unsafe.Add(ref src, srcIndex + 2);
        Unsafe.Add(ref dst, dstIndex + 3) = Unsafe.Add(ref src, srcIndex + 3);
        srcIndex += 4;
        dstIndex += 4;
        goto FoundLong;
    Found5:
        Copy(ref src, srcIndex, ref dst, dstIndex, 5);
        srcIndex += 5;
        dstIndex += 5;
        goto FoundLong;
    Found6:
        Copy(ref src, srcIndex, ref dst, dstIndex, 6);
        srcIndex += 6;
        dstIndex += 6;
        goto FoundLong;
    Found7:
        Copy(ref src, srcIndex, ref dst, dstIndex, 7);
        srcIndex += 7;
        dstIndex += 7;
        goto FoundLong;
    Found8:
        Copy(ref src, srcIndex, ref dst, dstIndex, 8);
        srcIndex += 8;
        dstIndex += 8;

    FoundLong:
        if (quote != Unsafe.Add(ref src, srcIndex)) goto Fail;

        srcIndex++;

        quotesLeft -= 2;

        if (quotesLeft <= 0) goto NoQuotesLeft;

    ContinueRead:
        while (TVector.IsSupported && srcIndex < vectorizedEnd)
        {
            TVector current = TVector.LoadUnaligned(ref src, (nuint)srcIndex);
            TVector equals = TVector.Equals(current, quoteVector);

            if (equals == TVector.Zero)
            {
                Copy(ref src, srcIndex, ref dst, dstIndex, (uint)TVector.Count);
                srcIndex += TVector.Count;
                dstIndex += TVector.Count;
                continue;
            }

            nuint mask = equals.ExtractMostSignificantBits();
            int charpos = BitOperations.TrailingZeroCount(mask) + 1;

            Copy(ref src, srcIndex, ref dst, dstIndex, (uint)charpos);
            srcIndex += charpos;
            dstIndex += charpos;
            goto FoundLong;
        }

        while (srcIndex < unrolledEnd)
        {
            if (quote == Unsafe.Add(ref src, srcIndex + 0)) goto Found1;
            if (quote == Unsafe.Add(ref src, srcIndex + 1)) goto Found2;
            if (quote == Unsafe.Add(ref src, srcIndex + 2)) goto Found3;
            if (quote == Unsafe.Add(ref src, srcIndex + 3)) goto Found4;
            if (quote == Unsafe.Add(ref src, srcIndex + 4)) goto Found5;
            if (quote == Unsafe.Add(ref src, srcIndex + 5)) goto Found6;
            if (quote == Unsafe.Add(ref src, srcIndex + 6)) goto Found7;
            if (quote == Unsafe.Add(ref src, srcIndex + 7)) goto Found8;

            Copy(ref src, srcIndex, ref dst, dstIndex, 8);
            srcIndex += 8;
            dstIndex += 8;
        }

        while (srcIndex < searchSpaceEnd)
        {
            if (quote == Unsafe.Add(ref src, srcIndex))
            {
                if (quote != Unsafe.Add(ref src, ++srcIndex)) goto Fail;

                Unsafe.Add(ref dst, dstIndex) = Unsafe.Add(ref src, srcIndex);
                srcIndex++;
                dstIndex++;

                quotesLeft -= 2;

                if (quotesLeft <= 0) goto NoQuotesLeft;
            }
            else
            {
                Unsafe.Add(ref dst, dstIndex) = Unsafe.Add(ref src, srcIndex);
                srcIndex++;
                dstIndex++;
            }
        }

        goto EOL;

        // Copy remaining data
    NoQuotesLeft:
        Copy(ref src, srcIndex, ref dst, dstIndex, (uint)(srcLength - srcIndex));

    EOL:
        if (quotesLeft != 0)
        {
            RFC4180Mode<T>.ThrowInvalidUnescape(field, quote, quotesConsumed);
        }

        return;

    Fail:
        RFC4180Mode<T>.ThrowInvalidUnescape(field, quote, quotesConsumed);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Copy(ref T src, nint srcIndex, ref T dst, nint dstIndex, uint length)
        {
            Unsafe.CopyBlockUnaligned(
                destination: ref Unsafe.As<T, byte>(ref Unsafe.Add(ref dst, dstIndex)),
                source: ref Unsafe.As<T, byte>(ref Unsafe.Add(ref src, srcIndex)),
                byteCount: (uint)Unsafe.SizeOf<T>() * length);
        }
    }
    }
}
#endif
