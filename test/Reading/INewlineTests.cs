using System;
using System.Buffers.Binary;
using System.Numerics; // for BitOperations
using FlameCsv.Extensions;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Tests.Reading;

public static class INewlineTests
{
    [Fact]
    public static void Should_Check_If_Newline()
    {
        Impl<char>("\n", true, isCRLF: false);
        Impl<char>("\r\n", true, isCRLF: true);
        Impl<char>("\r", true, isCRLF: false);
        Impl<char>("\n\r", true, isCRLF: false);
        Impl<byte>("\n"u8, true, isCRLF: false);
        Impl<byte>("\r\n"u8, true, isCRLF: true);
        Impl<byte>("\r"u8, true, isCRLF: false);
        Impl<byte>("\n\r"u8, true, isCRLF: false);

        // no other character should be a newline
        Span<char> chars = stackalloc char[2];
        Span<byte> bytes = stackalloc byte[2];

        for (int i = 1; i <= byte.MaxValue; i++)
        {
            if (i is '\n' or '\r')
            {
                continue;
            }

            chars[0] = (char)i;
            bytes[0] = (byte)i;

            Impl(chars[..1], false);
            Impl(bytes[..1], false);

            for (int j = 1; j <= byte.MaxValue; j++)
            {
                if (j is '\n' or '\r')
                {
                    continue;
                }

                chars[1] = (char)j;
                bytes[1] = (byte)j;

                Impl(chars[..2], false);
                Impl(bytes[..2], false);
            }
        }

        static void Impl<T>(ReadOnlySpan<T> input, bool expected, bool isCRLF = false)
            where T : unmanaged, IBinaryInteger<T>
        {
            // bigger span so we can read the second character if needed
            Span<T> span = stackalloc T[8];
            input.CopyTo(span);
            span = span[..input.Length];

            uint result = NewlineCRLF.GetNewlineFlag<T>(T.CreateTruncating(','), ref span[0]);

            if (expected)
            {
                Assert.True(((uint)result & Field.IsEOL) != 0, $"'{input.AsPrintableString()}' should be a newline");
                Assert.Equal(isCRLF, result == Field.IsCRLF);
            }
        }
    }

    [Fact]
    public static void IndexOfAnyTest()
    {
        byte[] data = "Hello, World! This is a test."u8.ToArray();
        int index = FastFinder.IndexOfAny4(data, (byte)',', (byte)'!');
        Assert.Equal(5, index);

        index = FastFinder.IndexOfAny4(data, (byte)'?', (byte)'!');
        Assert.Equal(12, index);

        index = FastFinder.IndexOfAny4(data, (byte)'x', (byte)'y');
        Assert.Equal(data.Length, index);
    }
}

public static class FastFinder
{
    // Search for any of (a,b,c,d) in byte[] data.
    // Returns the index of the first match, or data.Length if none found.
    public static int IndexOfAny4(byte[] data, byte a, byte b)
    {
        int length = data.Length;
        int index = 0;
        int unrolledEnd = length - (length % sizeof(ulong));

        ulong m0 = SWAR.Create(a);
        ulong m1 = SWAR.Create(b);
        ulong m2 = SWAR.Create((byte)'\n');
        ulong m3 = SWAR.Create((byte)'\r');

        // Unrolled, SWAR-based loop:
        for (; index < unrolledEnd; index += sizeof(ulong))
        {
            // Load 8 bytes little-endian
            ulong hay = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(index));

            // Build the SWAR mask for all four needles:
            ulong m = 0;

            ulong x0 = hay ^ m0;
            m |= (x0 - SWAR.Ones<byte>()) & ~x0 & SWAR.Highs<byte>();

            ulong x1 = hay ^ m1;
            m |= (x1 - SWAR.Ones<byte>()) & ~x1 & SWAR.Highs<byte>();

            ulong x2 = hay ^ m2;
            m |= (x2 - SWAR.Ones<byte>()) & ~x2 & SWAR.Highs<byte>();

            ulong x3 = hay ^ m3;
            m |= (x3 - SWAR.Ones<byte>()) & ~x3 & SWAR.Highs<byte>();

            if (m != 0)
            {
                index += SWAR.TrailingZeroCount<byte>(m);
                goto Found;
            }
        }

        // Scalar fallback for the tail
        for (; index < length; index++)
        {
            byte v = data[index];
            if (v == a || v == b || v == (byte)'\r' || v == (byte)'\n')
                break;
        }

        Found:
        // index == length => no match
        if (index < length)
        {
            byte match = data[index];
            if (match == a)
            { /* handle a */
            }
            else if (match == b)
            { /* handle b */
            }
            else if (match == '\r')
            { /* handle c */
            }
            else
            { /* handle d */
            }
        }

        return index;
    }
}
