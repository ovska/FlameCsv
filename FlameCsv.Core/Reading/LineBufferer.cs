using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;

namespace FlameCsv.Reading;

readonly struct BufferResult
{
    public int Consumed { get; init; }
    public int LinesRead { get; init; }
}

internal sealed class LineBufferer<T> where T : unmanaged, IEquatable<T>
{
    private readonly T _quote;
    private readonly T _newline1;
    private readonly T _newline2;

    public LineBufferer(CsvOptions<T> options)
    {
        _quote = options._quote;

        var span = options._newline.Span;
        _newline1 = span[0];
        _newline2 = span.Length == 2 ? span[1] : default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BufferResult ReadLines(
        ReadOnlySpan<T> data,
        scoped Span<CsvParser.Slice> slices,
        bool vectorized = false)
    {
        if (vectorized)
        {
            if (Unsafe.SizeOf<T>() == Unsafe.SizeOf<char>())
            {
                return RFC4180LineBufferer.ReadLinesCoreVectorized(
                    data.UnsafeCast<T, char>(),
                    slices,
                    GetAs<char>(_quote),
                    GetAs<char>(_newline1),
                    GetAs<char>(_newline2));
            }

            if (Unsafe.SizeOf<T>() == sizeof(byte))
            {
                throw new NotImplementedException();
                //CommunityToolkit.Diagnostics.Guard.IsTrue(SimdVector256<byte>.IsValid);
                //return RFC4180LineBufferer.ReadLinesCoreVectorized<byte, SimdVector256<byte>, Vector256<byte>>(
                //    data.UnsafeCast<T, byte>(),
                //    slices,
                //    GetAs<byte>(_quote),
                //    GetAs<byte>(_newline1),
                //    GetAs<byte>(_newline2));
            }
        }

        if (Unsafe.SizeOf<T>() == Unsafe.SizeOf<char>())
        {
            return RFC4180LineBufferer.ReadLinesCore(
                data.UnsafeCast<T, short>(),
                slices,
                GetAs<short>(_quote),
                GetAs<short>(_newline1),
                GetAs<short>(_newline2));
        }

        if (Unsafe.SizeOf<T>() == sizeof(byte))
        {
            return RFC4180LineBufferer.ReadLinesCore(
                data.UnsafeCast<T, byte>(),
                slices,
                GetAs<byte>(_quote),
                GetAs<byte>(_newline1),
                GetAs<byte>(_newline2));
        }

        throw new NotSupportedException();
    }

    private static TImpl GetAs<TImpl>(T value) where TImpl : unmanaged
        => Unsafe.As<T, TImpl>(ref Unsafe.AsRef(in value));
}

internal static class RFC4180LineBufferer
{
    public static BufferResult ReadLinesCoreVectorized(
        ReadOnlySpan<char> data,
        scoped Span<CsvParser.Slice> slices,
        char quote,
        char newline1,
        char newline2)
    {
        Debug.Assert(quote != newline1);
        Debug.Assert(quote != newline2);
        Debug.Assert(newline1 != newline2);

        int newlineLength = newline2.Equals(default) ? 1 : 2;
        int linesRead = 0;
        int consumed = 0;
        int currentConsumed = 0;

        CsvRecordMeta meta = default;

        var vQuote = Vector256.Create((byte)quote);
        var vLF = Vector256.Create((byte)newline1);

        while (linesRead < slices.Length)
        {
            int index;

            while (Vector256<byte>.Count <= data.Length)
            {
                ref byte byteRef = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(data));
                var v0 = Unsafe.ReadUnaligned<Vector256<short>>(ref byteRef);
                var v1 = Unsafe.ReadUnaligned<Vector256<short>>(ref Unsafe.Add(ref byteRef, Vector256<byte>.Count));

                var packed = Avx2.PackUnsignedSaturate(v0, v1);
                var bytes = Avx2.Permute4x64(packed.AsInt64(), 0b_11_01_10_00).AsByte();

                var quoteEq = Vector256.Equals(vQuote, bytes);
                var linefeedEq = Vector256.Equals(vLF, bytes);

                var specialChars = quoteEq | linefeedEq;
                var specialCharsMask = Avx2.MoveMask(specialChars);

                if (specialCharsMask != 0u)
                {
                    int quoteMask = Avx2.MoveMask(quoteEq);
                    int linefeedMask = Avx2.MoveMask(linefeedEq);

                    // just found quotes
                    if (linefeedMask == 0)
                    {
                        meta.quoteCount += Vector256.Sum(-quoteEq);
                        goto ContinueRead;
                    }

                    // just found a newline
                    if (quoteMask == 0)
                    {
                        index = BitOperations.TrailingZeroCount((uint)linefeedMask);
                        goto FoundNewline;
                    }

                    index = BitOperations.TrailingZeroCount((uint)specialCharsMask);

                    // found both, pick first index:
                    if (data.DangerousGetReferenceAt(index).Equals(newline1))
                    {
                        // its a newline and we're not in a string, continue
                        if (meta.quoteCount % 2 == 0)
                            goto FoundNewline;
                    }
                    else
                    {
                        meta.quoteCount++;
                    }

                    data = data.Slice(index + 1);
                    currentConsumed += index + 1;
                    continue;
                }

                ContinueRead:
                data = data.Slice(Vector256<byte>.Count);
                currentConsumed += Vector256<byte>.Count;
            }

            Seek:
            index = meta.quoteCount % 2 == 0
                ? data.IndexOfAny(quote, newline1)
                : data.IndexOf(quote);

            if (index < 0)
                break;

            if (data[index].Equals(quote))
            {
                meta.quoteCount++;
                data = data.Slice(index + 1);
                currentConsumed += index + 1;
                goto Seek;
            }

            FoundNewline:
            currentConsumed += index;

            if (newlineLength == 2)
            {
                // ran out of data
                if (data.Length <= index)
                    break;

                // next token wasn't the second newline
                if (!newline2.Equals(data[index + 1]))
                {
                    data = data.Slice(index + 1);
                    currentConsumed++;
                    goto Seek;
                }
            }

            // Found newline
            slices[linesRead++] = new CsvParser.Slice
            {
                Index = consumed,
                Length = currentConsumed,
                Meta = meta,
            };

            consumed += currentConsumed + newlineLength;
            data = data.Slice(index + newlineLength);
            currentConsumed = 0;
            meta = default;
        }

        return new BufferResult
        {
            Consumed = consumed,
            LinesRead = linesRead,
        };
    }

    public static BufferResult ReadLinesCore<T>(
        ReadOnlySpan<T> data,
        scoped Span<CsvParser.Slice> slices,
        T quote,
        T newline1,
        T newline2)
        where T : unmanaged, INumber<T>
    {
        int newlineLength = newline2.Equals(default) ? 1 : 2;
        int linesRead = 0;
        int consumed = 0;
        int currentConsumed = 0;

        CsvRecordMeta meta = default;

        while (linesRead < slices.Length)
        {
            Seek:
            int index = meta.quoteCount % 2 == 0
                    ? data.IndexOfAny(quote, newline1)
                    : data.IndexOf(quote);

            if (index < 0)
                break;

            if (data[index].Equals(quote))
            {
                meta.quoteCount++;
                data = data.Slice(index + 1);
                currentConsumed += index + 1;
                goto Seek;
            }

            currentConsumed += index;

            if (newlineLength == 2)
            {
                // ran out of data
                if (data.Length <= index)
                    break;

                // next token wasn't the second newline
                if (!newline2.Equals(data[index + 1]))
                {
                    data = data.Slice(index + 1);
                    currentConsumed++;
                    goto Seek;
                }
            }

            // Found newline
            slices[linesRead++] = new()
            {
                Index = consumed,
                Length = currentConsumed,
                Meta = meta,
            };

            consumed += currentConsumed + newlineLength;
            data = data.Slice(index + newlineLength);
            currentConsumed = 0;
            meta = default;
        }

        return new BufferResult
        {
            Consumed = consumed,
            LinesRead = linesRead,
        };
    }
}
