using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FlameCsv.Tests.Converters;

public static class SpanConvertableTests
{
    [Fact]
    public static void TranscodeParse()
    {
        Impl<Utf8Formattable>(false);
        Impl<Utf8Parsable>(false);
        Impl<Utf16Formattable>(false);
        Impl<Utf8Formattable>(true);
        Impl<Utf8Parsable>(true);
        Impl<Utf16Formattable>(true);
    }

    static void Impl<TType>(bool upperCase)
        where TType : struct, IValid
    {
        var options = new CsvOptions<byte>
        {
            Formats = { [typeof(TType)] = upperCase ? "U" : null },
            FormatProvider = upperCase ? CultureInfo.InvariantCulture : null,
        };
        var converter = options.GetConverter<TType>();

        Assert.False(converter.TryParse([], out _));
        Assert.False(converter.TryParse("aaaaaaaaaaaaaaaaaaaaaaaaaaa"u8, out _));

        Assert.True(converter.TryParse(upperCase ? "TEST"u8 : "test"u8, out var result));
        Assert.True(result.Valid);

        Assert.False(converter.TryParse(upperCase ? "test"u8 : "TEST"u8, out result));
        Assert.False(result.Valid);

        byte[] buffer = new byte[10];
        Assert.True(converter.TryFormat(buffer, result, out var bytesWritten));
        Assert.Equal(upperCase ? "TEST"u8 : "test"u8, buffer.AsSpan(0, bytesWritten));

        Assert.False(converter.TryFormat(buffer.AsSpan(0, 2), result, out bytesWritten));
    }

    private interface IValid
    {
        bool Valid { get; }
    }

    private readonly struct Utf16Formattable : ISpanFormattable, ISpanParsable<Utf16Formattable>, IValid
    {
        public bool Valid { get; init; }

        public static Utf16Formattable Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        {
            throw new NotImplementedException();
        }

        public static Utf16Formattable Parse(string s, IFormatProvider? provider)
        {
            throw new NotImplementedException();
        }

        public static bool TryParse(
            ReadOnlySpan<char> s,
            IFormatProvider? provider,
            [MaybeNullWhen(false)] out Utf16Formattable result
        )
        {
            if (s.SequenceEqual(provider is null ? "test" : "TEST"))
            {
                result = new() { Valid = true };
                return true;
            }

            result = default;
            return false;
        }

        public static bool TryParse(
            [NotNullWhen(true)] string? s,
            IFormatProvider? provider,
            [MaybeNullWhen(false)] out Utf16Formattable result
        )
        {
            return TryParse(s.AsSpan(), provider, out result);
        }

        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            throw new NotImplementedException();
        }

        public bool TryFormat(
            Span<char> destination,
            out int charsWritten,
            ReadOnlySpan<char> format,
            IFormatProvider? provider
        )
        {
            if ((format.IsEmpty ? "test" : "TEST").AsSpan().TryCopyTo(destination))
            {
                charsWritten = "test".Length;
                return true;
            }

            charsWritten = 0;
            return false;
        }
    }

    private readonly struct Utf8Formattable : IUtf8SpanFormattable, ISpanParsable<Utf8Formattable>, IValid
    {
        public bool Valid { get; init; }

        public static Utf8Formattable Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        {
            throw new NotImplementedException();
        }

        public static Utf8Formattable Parse(string s, IFormatProvider? provider)
        {
            throw new NotImplementedException();
        }

        public static bool TryParse(
            ReadOnlySpan<char> s,
            IFormatProvider? provider,
            [MaybeNullWhen(false)] out Utf8Formattable result
        )
        {
            if (s.SequenceEqual(provider is null ? "test" : "TEST"))
            {
                result = new() { Valid = true };
                return true;
            }

            result = default;
            return false;
        }

        public static bool TryParse(
            [NotNullWhen(true)] string? s,
            IFormatProvider? provider,
            [MaybeNullWhen(false)] out Utf8Formattable result
        )
        {
            return TryParse(s.AsSpan(), provider, out result);
        }

        public bool TryFormat(
            Span<byte> utf8Destination,
            out int bytesWritten,
            ReadOnlySpan<char> format,
            IFormatProvider? provider
        )
        {
            if ((format.IsEmpty ? "test"u8 : "TEST"u8).TryCopyTo(utf8Destination))
            {
                bytesWritten = "test".Length;
                return true;
            }

            bytesWritten = 0;
            return false;
        }
    }

    private readonly struct Utf8Parsable : ISpanFormattable, IUtf8SpanParsable<Utf8Parsable>, IValid
    {
        public bool Valid { get; init; }

        public static Utf8Parsable Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider)
        {
            throw new NotImplementedException();
        }

        public static bool TryParse(
            ReadOnlySpan<byte> utf8Text,
            IFormatProvider? provider,
            [MaybeNullWhen(false)] out Utf8Parsable result
        )
        {
            if (utf8Text.SequenceEqual(provider is null ? "test"u8 : "TEST"u8))
            {
                result = new() { Valid = true };
                return true;
            }

            result = default;
            return false;
        }

        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            throw new NotImplementedException();
        }

        public bool TryFormat(
            Span<char> destination,
            out int charsWritten,
            ReadOnlySpan<char> format,
            IFormatProvider? provider
        )
        {
            if ((format.IsEmpty ? "test" : "TEST").AsSpan().TryCopyTo(destination))
            {
                charsWritten = "test".Length;
                return true;
            }

            charsWritten = 0;
            return false;
        }
    }
}
