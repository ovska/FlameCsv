using System.Runtime.InteropServices;
using System.Text;
using CommunityToolkit.HighPerformance;
using FlameCsv.Attributes;

// ReSharper disable InconsistentNaming

namespace FlameCsv.Tests.Converters;

public static partial class EnumGeneratorTests
{
    private delegate CsvConverter<T, TEnum> Factory<T, TEnum>(bool numeric, bool ignoreCase)
        where T : unmanaged, IBinaryInteger<T>
        where TEnum : struct, Enum;

    private static CsvOptions<T> GetOpts<T>(bool numeric, bool ignoreCase) where T : unmanaged, IBinaryInteger<T>
    {
        return new CsvOptions<T> { EnumFormat = numeric ? "D" : "G", IgnoreEnumCase = ignoreCase };
    }

    [Fact]
    public static void NegativesChar()
    {
        Test_Negatives((numeric, ignoreCase) => new NegativeConverter_Char(GetOpts<char>(numeric, ignoreCase)));
    }

    [Fact]
    public static void NegativesByte()
    {
        Test_Negatives((numeric, ignoreCase) => new NegativeConverter_Byte(GetOpts<byte>(numeric, ignoreCase)));
    }

    [Fact]
    public static void AnimalChar()
    {
        Test_Animal((numeric, ignoreCase) => new AnimalConverter_Char(GetOpts<char>(numeric, ignoreCase)));
    }

    [Fact]
    public static void AnimalByte()
    {
        Test_Animal((numeric, ignoreCase) => new AnimalConverter_Byte(GetOpts<byte>(numeric, ignoreCase)));
    }

    [Fact]
    public static void DayOfWeekChar()
    {
        Test_DayOfWeek((numeric, ignoreCase) => new DayOfWeekConverter_Char(GetOpts<char>(numeric, ignoreCase)));
    }

    [Fact]
    public static void DayOfWeekByte()
    {
        Test_DayOfWeek((numeric, ignoreCase) => new DayOfWeekConverter_Byte(GetOpts<byte>(numeric, ignoreCase)));
    }

    [Fact]
    public static void NotAsciiChar()
    {
        Test_NotAscii((numeric, ignoreCase) => new NotAsciiConverter_Char(GetOpts<char>(numeric, ignoreCase)));
    }

    [Fact]
    public static void NotAsciiByte()
    {
        Test_NotAscii((numeric, ignoreCase) => new NotAsciiConverter_Byte(GetOpts<byte>(numeric, ignoreCase)));
    }

    private static void Test_Negatives<T>(Factory<T, Negatives> factory) where T : unmanaged, IBinaryInteger<T>
    {
        CheckParse(false, factory(numeric: false, ignoreCase: false));
        CheckParse(true, factory(numeric: false, ignoreCase: true));
        CheckFormat("D", factory(numeric: true, ignoreCase: false));
        CheckFormat("G", factory(numeric: false, ignoreCase: false));

        static void CheckFormat(string format, CsvConverter<T, Negatives> converter)
        {
            ImplFormat(Negatives.First, format, converter);
            ImplFormat(Negatives.Second, format, converter);
            ImplFormat(Negatives.Zero, format, converter);
            ImplFormat(Negatives.Zero2, format, converter);
            ImplFormat(Negatives.Two, format, converter);
        }

        static void CheckParse(bool ignoreCase, CsvConverter<T, Negatives> converter)
        {
            foreach (var value in Enum.GetValues<Negatives>())
            {
                ImplParse(value, value.ToString("G"), converter);
                ImplParse(value, value.ToString("D"), converter);
            }

            ImplNotParsable("-3", converter);
            ImplNotParsable("1", converter);
            ImplNotParsable("3", converter);

            if (ignoreCase)
            {
                ImplParse(Negatives.First, "first", converter);
                ImplParse(Negatives.First, "FIRST", converter);
                ImplParse(Negatives.First, "FIRST", converter);
                ImplParse(Negatives.First, "first", converter);
            }
            else
            {
                ImplNotParsable("first", converter);
                ImplNotParsable("FIRST", converter);
                ImplNotParsable("FIRST", converter);
                ImplNotParsable("first", converter);
            }
        }
    }

    private static void Test_Animal<T>(Factory<T, Animal> factory) where T : unmanaged, IBinaryInteger<T>
    {
        CheckParse(false, factory(numeric: false, ignoreCase: false));
        CheckParse(true, factory(numeric: false, ignoreCase: true));
        CheckFormat("D", factory(numeric: true, ignoreCase: false));
        CheckFormat("G", factory(numeric: false, ignoreCase: false));

        static void CheckFormat(string format, CsvConverter<T, Animal> converter)
        {
            ImplFormat(Animal.Dog, format, converter);
            ImplFormat(Animal.C4t, format, converter);
            ImplFormat(Animal.Bird, format, converter);
            ImplFormat(Animal.Fish, format, converter);
            ImplFormat(Animal.Rabbit, format, converter);
            ImplFormat(Animal.Elephant, format, converter);
            ImplFormat(Animal.Crocodile, format, converter);
            ImplFormat(Animal.Albatross, format, converter);
            ImplFormat(Animal.Chameleon, format, converter);
            ImplFormat(Animal.Platypus1, format, converter);
            ImplFormat(Animal.Com0doDr4gon, format, converter);
            ImplFormat(Animal.Zebra, format, converter, format == "G" ? "Zebra Animal!" : null);
            ImplFormat(Animal.SuperLongEnumNameThatGoesOnAndOn, format, converter);
        }

        static void CheckParse(bool ignoreCase, CsvConverter<T, Animal> converter)
        {
            foreach (var value in Enum.GetValues<Animal>())
            {
                ImplParse(value, value.ToString("G"), converter);
                ImplParse(value, value.ToString("D"), converter);
            }

            ImplParse(Animal.Zebra, "Zebra Animal!", converter);

            ImplNotParsable("-1", converter);
            ImplNotParsable("13", converter);

            if (ignoreCase)
            {
                ImplParse(Animal.Dog, "dOg", converter);
                ImplParse(Animal.Elephant, "eLEPHaNt", converter);
                ImplParse(Animal.Crocodile, "CROCODILE", converter);
                ImplParse(Animal.SuperLongEnumNameThatGoesOnAndOn, "superlongenumnamethatgoesonandon", converter);
                ImplParse(Animal.SuperLongEnumNameThatGoesOnAndOn, "SUPERLONGENUMNAMETHATGOESONANDON", converter);
                ImplParse(Animal.Zebra, "zebra", converter);
                ImplParse(Animal.Zebra, "ZEBRA", converter);
                ImplParse(Animal.Zebra, "ZEBRA ANIMAL!", converter);
                ImplParse(Animal.Zebra, "zebra animaL!", converter);
            }
            else
            {
                ImplNotParsable("dOg", converter);
                ImplNotParsable("eLEPHaNt", converter);
                ImplNotParsable("CROCODILE", converter);
                ImplNotParsable("superlongenumnamethatgoesonandon", converter);
                ImplNotParsable("SUPERLONGENUMNAMETHATGOESONANDON", converter);
                ImplNotParsable("zebra", converter);
                ImplNotParsable("ZEBRA", converter);
                ImplNotParsable("ZEBRA ANIMAL!", converter);
                ImplNotParsable("zebra animaL!", converter);
            }
        }
    }

    private static void Test_DayOfWeek<T>(Factory<T, DayOfWeek> factory) where T : unmanaged, IBinaryInteger<T>
    {
        CheckParse(false, factory(numeric: false, ignoreCase: false));
        CheckParse(true, factory(numeric: false, ignoreCase: true));
        CheckFormat("D", factory(numeric: true, ignoreCase: false));
        CheckFormat("G", factory(numeric: false, ignoreCase: false));

        static void CheckFormat(string format, CsvConverter<T, DayOfWeek> converter)
        {
            ImplFormat(DayOfWeek.Monday, format, converter);
            ImplFormat(DayOfWeek.Tuesday, format, converter);
            ImplFormat(DayOfWeek.Wednesday, format, converter);
            ImplFormat(DayOfWeek.Thursday, format, converter);
            ImplFormat(DayOfWeek.Friday, format, converter);
            ImplFormat(DayOfWeek.Saturday, format, converter);
            ImplFormat(DayOfWeek.Sunday, format, converter);
        }

        static void CheckParse(bool ignoreCase, CsvConverter<T, DayOfWeek> converter)
        {
            foreach (var value in Enum.GetValues<DayOfWeek>())
            {
                ImplParse(value, value.ToString("G"), converter);
                ImplParse(value, value.ToString("D"), converter);
            }

            ImplNotParsable("-1", converter);
            ImplNotParsable("7", converter);

            if (ignoreCase)
            {
                ImplParse(DayOfWeek.Monday, "moNdAy", converter);
                ImplParse(DayOfWeek.Monday, "MONDAY", converter);
                ImplParse(DayOfWeek.Monday, "monday", converter);
            }
            else
            {
                ImplNotParsable("moNdAy", converter);
                ImplNotParsable("MONDAY", converter);
                ImplNotParsable("monday", converter);
            }
        }
    }

    private static void Test_NotAscii<T>(Factory<T, NotAscii> factory) where T : unmanaged, IBinaryInteger<T>
    {
        CheckParse(false, factory(numeric: false, ignoreCase: false));
        CheckParse(true, factory(numeric: false, ignoreCase: true));
        CheckFormat("D", factory(numeric: true, ignoreCase: false));
        CheckFormat("G", factory(numeric: false, ignoreCase: false));

        static void CheckFormat(string format, CsvConverter<T, NotAscii> converter)
        {
            ImplFormat(NotAscii.Unicorn, format, converter, "🦄");
            ImplFormat(NotAscii.Dragon, format, converter, "🐉");
            ImplFormat(NotAscii.Café, format, converter);
            ImplFormat(NotAscii.Meat, format, converter, "🥩🥩🥩🥩🥩🥩🥩🥩");
        }

        static void CheckParse(bool ignoreCase, CsvConverter<T, NotAscii> converter)
        {
            foreach (var value in Enum.GetValues<NotAscii>())
            {
                ImplParse(value, value.ToString("G"), converter);
                ImplParse(value, value.ToString("D"), converter);
            }

            ImplNotParsable("-1", converter);
            ImplNotParsable("4", converter);

            ImplParse(NotAscii.Unicorn, "🦄", converter);
            ImplParse(NotAscii.Dragon, "🐉", converter);
            ImplParse(NotAscii.Meat, "🥩🥩🥩🥩🥩🥩🥩🥩", converter);

            if (ignoreCase)
            {
                ImplParse(NotAscii.Café, "café", converter);
                ImplParse(NotAscii.Café, "CAFÉ", converter);
            }
            else
            {
                ImplNotParsable("café", converter);
                ImplNotParsable("CAFÉ", converter);
            }
        }
    }

    private static void ImplParse<T, TEnum>(TEnum value, string expected, CsvConverter<T, TEnum> converter)
        where T : unmanaged, IBinaryInteger<T> // char or byte
        where TEnum : struct, Enum
    {
        if (!converter.TryParse(ToT<T>(expected), out var parsed))
        {
            Assert.Fail($"Could not parse '{expected}' into {value}.");
        }

        Assert.Equal(value, parsed);
    }

    private static void ImplFormat<T, TEnum>(
        TEnum value,
        string format,
        CsvConverter<T, TEnum> converter,
        string? expected = null)
        where T : unmanaged, IBinaryInteger<T> // char or byte
        where TEnum : struct, Enum
    {
        if (expected is null || format == "D")
        {
            expected = value.ToString(format);
        }

        int length = typeof(T) == typeof(byte) ? Encoding.UTF8.GetByteCount(expected) : expected.Length;

        T[] buffer = new T[length + 1];

        Assert.True(converter.TryFormat(buffer, value, out var written));
        Assert.Equal(length, written);
        Assert.Equal(expected, FromT(buffer.AsSpan(0, written)));
        Assert.Equal(T.Zero, buffer[written]); // don't write past the end
    }

    private static void ImplNotParsable<T, TEnum>(string value, CsvConverter<T, TEnum> converter)
        where T : unmanaged, IBinaryInteger<T> // char or byte
        where TEnum : struct, Enum
    {
        if (converter.TryParse(ToT<T>(value), out _))
        {
            Assert.Fail($"Value '{value}' should not be parsable to {typeof(TEnum).Name}.");
        }
    }

    private static string FromT<T>(Span<T> value) where T : unmanaged
    {
        Assert.True(typeof(T) == typeof(byte) || typeof(T) == typeof(char));
        return typeof(T) == typeof(byte)
            ? Encoding.UTF8.GetString(MemoryMarshal.Cast<T, byte>(value))
            : value.ToString();
    }

    private static ReadOnlySpan<T> ToT<T>(string value) where T : unmanaged
    {
        Assert.True(typeof(T) == typeof(byte) || typeof(T) == typeof(char));
        return typeof(T) == typeof(byte)
            ? MemoryMarshal.Cast<byte, T>(Encoding.UTF8.GetBytes(value))
            : MemoryMarshal.Cast<char, T>(value.AsSpan());
    }

    [CsvEnumConverter<char, DayOfWeek>]
    private partial class DayOfWeekConverter_Char;

    [CsvEnumConverter<byte, DayOfWeek>]
    private partial class DayOfWeekConverter_Byte;

    [CsvEnumConverter<char, Animal>]
    private partial class AnimalConverter_Char;

    [CsvEnumConverter<byte, Animal>]
    private partial class AnimalConverter_Byte;

    [CsvEnumConverter<char, Negatives>]
    private partial class NegativeConverter_Char;

    [CsvEnumConverter<byte, Negatives>]
    private partial class NegativeConverter_Byte;

    [CsvEnumConverter<char, NotAscii>]
    private partial class NotAsciiConverter_Char;

    [CsvEnumConverter<byte, NotAscii>]
    private partial class NotAsciiConverter_Byte;

    private enum Animal
    {
        Dog = 0,
        C4t = 1,
        Bird = 2,
        Fish = 3,
        Rabbit = 4,
        Elephant = 5,
        Crocodile = 6,
        Albatross = 7,
        Chameleon = 8,
        Platypus1 = 9,
        Com0doDr4gon = 10,

        [global::System.Runtime.Serialization.EnumMember(Value = "Zebra Animal!")]
        Zebra = 11,

        SuperLongEnumNameThatGoesOnAndOn = 12,
    }

    private enum Negatives
    {
        First = -1,
        Second = -2,
        Zero = 0,
        Zero2 = Zero,
        Two = 2,
    }

    private enum NotAscii
    {
        [global::System.Runtime.Serialization.EnumMember(Value = "🦄")]
        Unicorn = 0,

        [global::System.Runtime.Serialization.EnumMember(Value = "🐉")]
        Dragon = 1,

        Café = 2,

        [global::System.Runtime.Serialization.EnumMember(Value = "🥩🥩🥩🥩🥩🥩🥩🥩")]
        Meat = 0xBEEF,
    }
}
