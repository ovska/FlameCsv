using System.Runtime.Serialization;
using System.Text;
using FlameCsv.Converters.Enums;
using FlameCsv.Reflection;
using FlameCsv.Utilities.Comparers;

// ReSharper disable UnusedMember.Global

namespace FlameCsv.Tests.Converters;

public static class OldEnumTests
{
    public enum CustomDayOfWeek
    {
        [EnumMember(Value = "Möndäy")]
        Monday = 0,

        [EnumMember(Value = "Tuesday")]
        Tuesday = 1,

        [EnumMember(Value = "wednesdaY")]
        Wednesday = 2,

        [EnumMember(Value = "Thu, rs, day!")]
        Thursday = 3,

        [EnumMember(Value = "_6")]
        Friday = 4,

        [EnumMember(Value = "Säturday")]
        Saturday = 5,
        Sunday = 6,
        Negative = -255,
        Large = 1234,
    }

    [Fact]
    public static void Text()
    {
        Assertions(
            ignoreCase => new EnumTextConverter<DayOfWeek>(new CsvOptions<char> { IgnoreEnumCase = ignoreCase }),
            ignoreCase => new EnumTextConverter<CustomDayOfWeek>(new CsvOptions<char> { IgnoreEnumCase = ignoreCase })
        );
    }

    [Fact]
    public static void Utf8()
    {
        Assertions(
            ignoreCase => new EnumUtf8Converter<DayOfWeek>(new CsvOptions<byte> { IgnoreEnumCase = ignoreCase }),
            ignoreCase => new EnumUtf8Converter<CustomDayOfWeek>(new CsvOptions<byte> { IgnoreEnumCase = ignoreCase })
        );
    }

    private static void Assertions<T>(
        Func<bool, CsvConverter<T, DayOfWeek>> getCache,
        Func<bool, CsvConverter<T, CustomDayOfWeek>> getCacheCustom
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        CsvConverter<T, DayOfWeek> ordinalCase = getCache(false);

        Assert.True(ordinalCase.TryParse(ToT<T>("Monday"), out DayOfWeek monday));
        Assert.Equal(DayOfWeek.Monday, monday);
        Assert.True(ordinalCase.TryParse(ToT<T>("1"), out monday));
        Assert.Equal(DayOfWeek.Monday, monday);

        Assert.True(ordinalCase.TryParse(ToT<T>("Tuesday"), out DayOfWeek tuesday));
        Assert.Equal(DayOfWeek.Tuesday, tuesday);
        Assert.True(ordinalCase.TryParse(ToT<T>("2"), out tuesday));
        Assert.Equal(DayOfWeek.Tuesday, tuesday);

        Assert.False(ordinalCase.TryParse(ToT<T>("MONDAY"), out _));
        Assert.False(ordinalCase.TryParse(ToT<T>("tuesday"), out _));

        Assert.False(ordinalCase.TryParse(ToT<T>("Möndäy"), out _));

        CsvConverter<T, DayOfWeek> ignoreCase = getCache(true);

        Assert.True(ignoreCase.TryParse(ToT<T>("Monday"), out monday));
        Assert.Equal(DayOfWeek.Monday, monday);

        Assert.True(ignoreCase.TryParse(ToT<T>("Tuesday"), out tuesday));
        Assert.Equal(DayOfWeek.Tuesday, tuesday);

        Assert.True(ignoreCase.TryParse(ToT<T>("MONDAY"), out monday));
        Assert.Equal(DayOfWeek.Monday, monday);

        Assert.True(ignoreCase.TryParse(ToT<T>("tuesday"), out tuesday));
        Assert.Equal(DayOfWeek.Tuesday, tuesday);

        CsvConverter<T, CustomDayOfWeek> customOrdinal = getCacheCustom(false);

        Assert.True(customOrdinal.TryParse(ToT<T>("Monday"), out CustomDayOfWeek monday2));
        Assert.Equal(CustomDayOfWeek.Monday, monday2);
        Assert.True(customOrdinal.TryParse(ToT<T>("0"), out monday2));
        Assert.Equal(CustomDayOfWeek.Monday, monday2);

        Assert.True(customOrdinal.TryParse(ToT<T>("Tuesday"), out CustomDayOfWeek tuesday2));
        Assert.Equal(CustomDayOfWeek.Tuesday, tuesday2);

        Assert.False(customOrdinal.TryParse(ToT<T>("MONDAY"), out _));
        Assert.False(customOrdinal.TryParse(ToT<T>("tuesday"), out _));

        Assert.True(customOrdinal.TryParse(ToT<T>("wednesdaY"), out CustomDayOfWeek wednesday));
        Assert.Equal(CustomDayOfWeek.Wednesday, wednesday);

        Assert.False(customOrdinal.TryParse(ToT<T>("wednesdAy"), out _));

        Assert.True(customOrdinal.TryParse(ToT<T>("Möndäy"), out monday2));
        Assert.Equal(CustomDayOfWeek.Monday, monday2);
        Assert.False(customOrdinal.TryParse(ToT<T>("MÖNDÄY"), out _));

        Assert.True(customOrdinal.TryParse(ToT<T>("Thu, rs, day!"), out CustomDayOfWeek thursday));
        Assert.Equal(CustomDayOfWeek.Thursday, thursday);
        Assert.False(customOrdinal.TryParse(ToT<T>("THU, RS, DAY!"), out _));

        Assert.True(customOrdinal.TryParse(ToT<T>("_6"), out CustomDayOfWeek friday));
        Assert.Equal(CustomDayOfWeek.Friday, friday);

        CsvConverter<T, CustomDayOfWeek> customIgnoreCase = getCacheCustom(true);

        Assert.True(customIgnoreCase.TryParse(ToT<T>("Monday"), out monday2));
        Assert.Equal(CustomDayOfWeek.Monday, monday2);

        Assert.True(customIgnoreCase.TryParse(ToT<T>("Tuesday"), out tuesday2));
        Assert.Equal(CustomDayOfWeek.Tuesday, tuesday2);

        Assert.True(customIgnoreCase.TryParse(ToT<T>("MONDAY"), out monday2));
        Assert.Equal(CustomDayOfWeek.Monday, monday2);
        Assert.True(customIgnoreCase.TryParse(ToT<T>("tuesday"), out tuesday2));
        Assert.Equal(CustomDayOfWeek.Tuesday, tuesday2);

        Assert.True(customIgnoreCase.TryParse(ToT<T>("wednesdaY"), out wednesday));
        Assert.Equal(CustomDayOfWeek.Wednesday, wednesday);

        Assert.True(customIgnoreCase.TryParse(ToT<T>("wednesdAy"), out wednesday));
        Assert.Equal(CustomDayOfWeek.Wednesday, wednesday);

        Assert.True(customIgnoreCase.TryParse(ToT<T>("Möndäy"), out monday2));
        Assert.Equal(CustomDayOfWeek.Monday, monday2);
        Assert.True(customIgnoreCase.TryParse(ToT<T>("MÖNDÄY"), out monday2));
        Assert.Equal(CustomDayOfWeek.Monday, monday2);

        Assert.True(customIgnoreCase.TryParse(ToT<T>("Thu, rs, day!"), out thursday));
        Assert.Equal(CustomDayOfWeek.Thursday, thursday);
        Assert.True(customIgnoreCase.TryParse(ToT<T>("THU, RS, DAY!"), out thursday));
        Assert.Equal(CustomDayOfWeek.Thursday, thursday);

        Assert.True(customIgnoreCase.TryParse(ToT<T>("_6"), out friday));
        Assert.Equal(CustomDayOfWeek.Friday, friday);
    }

    [Theory]
    [InlineData("Monday")]
    [InlineData("Tues, day")]
    [InlineData("tESt")]
    public static void AsciiIgnoreCase(string value)
    {
        TestBytes(IgnoreCaseAsciiComparer.Instance, value);
    }

    [Theory]
    [InlineData("Monday")]
    [InlineData("Tues, day")]
    [InlineData("tESt")]
    [InlineData("Möndäy")]
    [InlineData("Röhkivä € Rähinä !!!")]
    public static void Utf8IgnoreCase(string value)
    {
        TestBytes(Utf8Comparer.OrdinalIgnoreCase, value);
    }

    private static void TestBytes(IEqualityComparer<StringLike> comparer, string value)
    {
        string upper = value.ToUpperInvariant();
        string lower = value.ToLowerInvariant();

        Assert.Equal(value, upper, comparer);
        Assert.Equal(value, lower, comparer);
        Assert.Equal(upper, lower, comparer);

        IAlternateEqualityComparer<ReadOnlySpan<byte>, StringLike> alternate =
            (IAlternateEqualityComparer<ReadOnlySpan<byte>, StringLike>)comparer;

        Assert.True(alternate.Equals(Encoding.UTF8.GetBytes(value), value));
        Assert.True(alternate.Equals(Encoding.UTF8.GetBytes(upper), value));
        Assert.True(alternate.Equals(Encoding.UTF8.GetBytes(lower), value));

        Assert.Equal(comparer.GetHashCode(value), comparer.GetHashCode(upper));

        Assert.Equal(comparer.GetHashCode(value), comparer.GetHashCode(lower));

        Assert.Equal(comparer.GetHashCode(value), alternate.GetHashCode(Encoding.UTF8.GetBytes(value)));

        Assert.Equal(comparer.GetHashCode(value), alternate.GetHashCode(Encoding.UTF8.GetBytes(upper)));

        Assert.Equal(comparer.GetHashCode(value), alternate.GetHashCode(Encoding.UTF8.GetBytes(lower)));
    }

    [Fact]
    public static void WriteText()
    {
        WriteImpl(f => new EnumTextConverter<CustomDayOfWeek>(new CsvOptions<char> { EnumFormat = f }));
    }

    [Fact]
    public static void WriteUtf8()
    {
        WriteImpl(f => new EnumUtf8Converter<CustomDayOfWeek>(new CsvOptions<byte> { EnumFormat = f }));
    }

    private static void WriteImpl<T>(Func<string, CsvConverter<T, CustomDayOfWeek>> getCache)
        where T : unmanaged, IBinaryInteger<T>
    {
        var numericCache = getCache("D");

        Assert.True(numericCache.TryGetName(CustomDayOfWeek.Monday, out ReadOnlySpan<T> span));
        Assert.Equal("0", FromT(span));

        Assert.True(numericCache.TryGetName(CustomDayOfWeek.Tuesday, out span));
        Assert.Equal("1", FromT(span));

        var stringCache = getCache("G");

        Assert.True(stringCache.TryGetName(CustomDayOfWeek.Monday, out span));
        Assert.Equal("Möndäy", FromT(span));

        Assert.True(stringCache.TryGetName(CustomDayOfWeek.Tuesday, out span));
        Assert.Equal("Tuesday", FromT(span));

        Assert.True(stringCache.TryGetName(CustomDayOfWeek.Wednesday, out span));
        Assert.Equal("wednesdaY", FromT(span));

        var hex = getCache("X");

        Assert.True(hex.TryGetName(CustomDayOfWeek.Monday, out span));
        Assert.Equal("00000000", FromT(span));

        Assert.True(hex.TryGetName(CustomDayOfWeek.Large, out span));
        Assert.Equal("000004D2", FromT(span));
    }

    private static ReadOnlySpan<T> ToT<T>(string value)
        where T : unmanaged, IBinaryInteger<T>
    {
        return CsvOptions<T>.Default.GetFromString(value).Span;
    }

    private static string FromT<T>(ReadOnlySpan<T> span)
        where T : unmanaged, IBinaryInteger<T>
    {
        return CsvOptions<T>.Default.GetAsString(span);
    }
}

file static class Extensions
{
    public static bool TryGetName<T, TValue>(
        this CsvConverter<T, TValue> converter,
        TValue value,
        out ReadOnlySpan<T> span
    )
        where T : unmanaged, IBinaryInteger<T>
        where TValue : struct, Enum
    {
        Span<T> buffer = new T[32];

        if (converter.TryFormat(buffer, value, out int written))
        {
            span = buffer.Slice(0, written);
            return true;
        }

        span = default;
        return false;
    }
}
