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

        // no other ascii character should be a newlinew
        Span<char> chars = stackalloc char[2];
        Span<byte> bytes = stackalloc byte[2];

        for (int i = 1; i < 128; i++)
        {
            if (i is '\n' or '\r')
            {
                continue;
            }

            chars[0] = (char)i;
            bytes[0] = (byte)i;

            Impl(chars[..1], false);
            Impl(bytes[..1], false);

            for (int j = 1; j < 128; j++)
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
            Span<T> span = stackalloc T[2];
            input.CopyTo(span);
            span = span[..input.Length];

            bool result = NewlineCRLF.IsNewline(ref span[0], out bool isMultitoken);

            if (expected)
            {
                Assert.True(NewlineCRLF.IsNewline(span[0]));
                Assert.True(result, $"'{input.AsPrintableString()}' should be a newline");
                Assert.Equal(isCRLF, isMultitoken);
                Assert.Equal(isCRLF, NewlineCRLF.IsMultitoken(ref span[0]));
            }
            else
            {
                if (result != false)
                {
                    
                }
                
                Assert.False(result, $"'{input.AsPrintableString()}' should not be a newline");
                Assert.False(NewlineCRLF.IsNewline(span[0]));
            }
        }
    }
}
