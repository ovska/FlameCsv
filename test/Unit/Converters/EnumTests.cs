using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FlameCsv.Reading;

// ReSharper disable InconsistentNaming

namespace FlameCsv.Tests.Converters;

public abstract class EnumTests<T> where T : unmanaged, IBinaryInteger<T>
{
    private delegate CsvConverter<T, TEnum> Factory<TEnum>(bool numeric, bool ignoreCase) where TEnum : struct, Enum;

    protected static CsvOptions<T> GetOpts(bool numeric, bool ignoreCase)
    {
        return new CsvOptions<T>
        {
            EnumFormat = numeric ? "D" : "G",
            IgnoreEnumCase = ignoreCase,
            EnumFlagsSeparator = '|',
            Trimming = CsvFieldTrimming.Both,
        };
    }

    protected abstract CsvConverter<T, DayOfWeek> GetDayOfWeek(bool numeric, bool ignoreCase);
    protected abstract CsvConverter<T, Animal> GetAnimal(bool numeric, bool ignoreCase);
    protected abstract CsvConverter<T, Negatives> GetNegatives(bool numeric, bool ignoreCase);
    protected abstract CsvConverter<T, NotAscii> GetNotAscii(bool numeric, bool ignoreCase);
    protected abstract CsvConverter<T, FlagsEnum> GetFlagsEnum(bool numeric, bool ignoreCase, bool allowUndefined);

    [Fact]
    public void Test_Negatives()
    {
        CheckParse(false, GetNegatives(numeric: false, ignoreCase: false));
        CheckParse(true, GetNegatives(numeric: false, ignoreCase: true));
        CheckFormat("D", GetNegatives(numeric: true, ignoreCase: false));
        CheckFormat("G", GetNegatives(numeric: false, ignoreCase: false));

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

    [Fact]
    public void Test_Animal()
    {
        CheckParse(false, GetAnimal(numeric: false, ignoreCase: false));
        CheckParse(true, GetAnimal(numeric: false, ignoreCase: true));
        CheckFormat("D", GetAnimal(numeric: true, ignoreCase: false));
        CheckFormat("G", GetAnimal(numeric: false, ignoreCase: false));

        void CheckFormat(string format, CsvConverter<T, Animal> converter)
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
            ImplFormat(Animal.SuperLongEnumNameThatGoesOnAndOn, format, converter);
            ImplFormat(Animal.Zebra, format, converter, format == "G" ? "Zebra Animal!" : null);
        }

        void CheckParse(bool ignoreCase, CsvConverter<T, Animal> converter)
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

    [Fact]
    public void Test_DayOfWeek()
    {
        CheckParse(false, GetDayOfWeek(numeric: false, ignoreCase: false));
        CheckParse(true, GetDayOfWeek(numeric: false, ignoreCase: true));
        CheckFormat("D", GetDayOfWeek(numeric: true, ignoreCase: false));
        CheckFormat("G", GetDayOfWeek(numeric: false, ignoreCase: false));

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

    [Fact]
    public void Test_NotAscii()
    {
        CheckParse(false, GetNotAscii(numeric: false, ignoreCase: false));
        CheckParse(true, GetNotAscii(numeric: false, ignoreCase: true));
        CheckFormat("D", GetNotAscii(numeric: true, ignoreCase: false));
        CheckFormat("G", GetNotAscii(numeric: false, ignoreCase: false));

        void CheckFormat(string format, CsvConverter<T, NotAscii> converter)
        {
            ImplFormat(NotAscii.Café, format, converter);
            ImplFormat(NotAscii.Unicorn, format, converter, "🦄");
            ImplFormat(NotAscii.Dragon, format, converter, "🐉");
            ImplFormat(NotAscii.Meat, format, converter, "🥩🥩🥩🥩🥩🥩🥩🥩");
        }

        void CheckParse(bool ignoreCase, CsvConverter<T, NotAscii> converter)
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

    [Fact]
    public void Test_Flags()
    {
        CheckParse(false, GetFlagsEnum(numeric: false, ignoreCase: false, allowUndefined: false));
        CheckParse(true, GetFlagsEnum(numeric: false, ignoreCase: true, allowUndefined: false));
        CheckFormat("D", GetFlagsEnum(numeric: true, ignoreCase: false, allowUndefined: false));
        CheckFormat("G", GetFlagsEnum(numeric: false, ignoreCase: false, allowUndefined: false));
        CheckFlags(numeric: false, ignoreCase: false);
        CheckFlags(numeric: false, ignoreCase: true);
        CheckFlags(numeric: true, ignoreCase: false);

        void CheckFormat(string format, CsvConverter<T, FlagsEnum> converter)
        {
            ImplFormat(FlagsEnum.None, format, converter);
            ImplFormat(FlagsEnum.First, format, converter);
            ImplFormat(FlagsEnum.Second, format, converter);
            ImplFormat(FlagsEnum.SecondAndThird, format, converter);
            ImplFormat(FlagsEnum.CustomName, format, converter, "Custom Name");
        }

        void CheckParse(bool ignoreCase, CsvConverter<T, FlagsEnum> converter)
        {
            ImplParse(FlagsEnum.None, "None", converter);
            ImplParse(FlagsEnum.First, "First", converter);
            ImplParse(FlagsEnum.Second, "Second", converter);
            ImplParse(FlagsEnum.Third, "Third", converter);
            ImplParse(FlagsEnum.SecondAndThird, "SecondAndThird", converter);
            ImplParse(FlagsEnum.CustomName, "CustomName", converter);

            ImplParse(FlagsEnum.CustomName, "Custom Name", converter);
            if (ignoreCase) ImplParse(FlagsEnum.CustomName, "cuSToM NaME", converter);
            else ImplNotParsable("cuSToM NaME", converter);

            if (ignoreCase)
            {
                ImplParse(FlagsEnum.None, "NONE", converter);
                ImplParse(FlagsEnum.First, "FIRST", converter);
                ImplParse(FlagsEnum.Second, "SECOND", converter);
                ImplParse(FlagsEnum.Third, "THIRD", converter);
                ImplParse(FlagsEnum.SecondAndThird, "SECONDANDTHIRD", converter);
                ImplParse(FlagsEnum.CustomName, "CUSTOMNAME", converter);
            }
            else
            {
                ImplNotParsable("NONE", converter);
                ImplNotParsable("FIRST", converter);
                ImplNotParsable("SECOND", converter);
                ImplNotParsable("THIRD", converter);
                ImplNotParsable("SECONDANDTHIRD", converter);
                ImplNotParsable("CUSTOMNAME", converter);
            }
        }

        void CheckFlags(bool numeric, bool ignoreCase)
        {
            var converter = GetFlagsEnum(numeric: numeric, ignoreCase: ignoreCase, allowUndefined: false);
            ImplParse(FlagsEnum.None, "0", converter);
            ImplParse(FlagsEnum.First, "1", converter);
            ImplParse(FlagsEnum.Second, "2", converter);
            ImplParse(FlagsEnum.First | FlagsEnum.Second, "3", converter);
            ImplParse(FlagsEnum.Third, "4", converter);
            ImplParse(FlagsEnum.SecondAndThird, "6", converter);
            ImplParse(FlagsEnum.CustomName, "8", converter);
            ImplNotParsable("16", converter); // not defined

            ImplParse(FlagsEnum.None, "None", converter);
            ImplParse(FlagsEnum.First, "First", converter);
            ImplParse(FlagsEnum.First | FlagsEnum.Second, "First|Second", converter);
            ImplParse(FlagsEnum.Third, "Third", converter);
            ImplParse(FlagsEnum.SecondAndThird, "SecondAndThird", converter);
            ImplParse(FlagsEnum.SecondAndThird, "Second|Third", converter);
            ImplParse(FlagsEnum.CustomName, "CustomName", converter);
            ImplParse(FlagsEnum.CustomName, "Custom Name", converter);
            ImplParse(FlagsEnum.First | FlagsEnum.CustomName, "First|Custom Name", converter);
            ImplParse(FlagsEnum.First, "None|First|First", converter);

            converter = GetFlagsEnum(numeric: numeric, ignoreCase: ignoreCase, allowUndefined: true);
            ImplParse((FlagsEnum)16, "16", converter);

            string format = numeric ? "D" : "F";
            ImplFormat(FlagsEnum.None, format, converter);
            ImplFormat(FlagsEnum.First, format, converter);
            ImplFormat(FlagsEnum.Second, format, converter);
            ImplFormat(FlagsEnum.First | FlagsEnum.Second, format, converter, "First|Second");
            ImplFormat(FlagsEnum.Third, format, converter);
            ImplFormat(FlagsEnum.SecondAndThird, format, converter, "SecondAndThird");
            ImplFormat(FlagsEnum.CustomName, format, converter, "Custom Name");
            ImplFormat(FlagsEnum.First | FlagsEnum.CustomName, format, converter, "First|Custom Name");
            ImplFormat(
                FlagsEnum.First | FlagsEnum.Second | FlagsEnum.Third,
                format,
                converter,
                "First|SecondAndThird");
            ImplFormat((FlagsEnum)16, format, converter);
        }
    }

    [StackTraceHidden]
    private static void ImplParse<TEnum>(TEnum value, string expected, CsvConverter<T, TEnum> converter)
        where TEnum : struct, Enum
    {
        if (!converter.TryParse(ToT(expected), out var parsed))
        {
            Assert.Fail($"Could not parse '{expected}' into {value}.");
        }

        Assert.Equal(value, parsed);
    }

    private static void ImplFormat<TEnum>(
        TEnum value,
        string format,
        CsvConverter<T, TEnum> converter,
        string? expected = null)
        where TEnum : struct, Enum
    {
        if (expected is null || format == "D")
        {
            expected = value.ToString(format);
        }

        int length = typeof(T) == typeof(byte) ? Encoding.UTF8.GetByteCount(expected) : expected.Length;

        T[] buffer = new T[length + 1];

        Assert.True(converter.TryFormat(buffer, value, out var written));
        Assert.Equal(expected, FromT(buffer.AsSpan(0, written)));
        Assert.Equal(length, written);
        Assert.Equal(T.Zero, buffer[written]); // don't write past the end
    }

    private static void ImplNotParsable<TEnum>(string value, CsvConverter<T, TEnum> converter)
        where TEnum : struct, Enum
    {
        if (converter.TryParse(ToT(value), out _))
        {
            Assert.Fail($"Value '{value}' should not be parsable to {typeof(TEnum).Name}.");
        }
    }

    private static string FromT(Span<T> value)
    {
        Assert.True(typeof(T) == typeof(byte) || typeof(T) == typeof(char));
        return typeof(T) == typeof(byte)
            ? Encoding.UTF8.GetString(MemoryMarshal.Cast<T, byte>(value))
            : value.ToString();
    }

    private static ReadOnlySpan<T> ToT(string value)
    {
        Assert.True(typeof(T) == typeof(byte) || typeof(T) == typeof(char));
        return typeof(T) == typeof(byte)
            ? MemoryMarshal.Cast<byte, T>(Encoding.UTF8.GetBytes(value))
            : MemoryMarshal.Cast<char, T>(value.AsSpan());
    }

    protected enum Animal
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

    protected enum Negatives
    {
        First = -1,
        Second = -2,
        Zero = 0,
        Zero2 = Zero,
        Two = 2,
    }

    protected enum NotAscii
    {
        [global::System.Runtime.Serialization.EnumMember(Value = "🦄")]
        Unicorn = 0,

        [global::System.Runtime.Serialization.EnumMember(Value = "🐉")]
        Dragon = 1,

        Café = 2,

        [global::System.Runtime.Serialization.EnumMember(Value = "🥩🥩🥩🥩🥩🥩🥩🥩")]
        Meat = 0xBEEF,
    }

    [Flags]
    protected enum FlagsEnum
    {
        None = 0,
        First = 1 << 0,
        Second = 1 << 1,
        Third = 1 << 2,
        SecondAndThird = Second | Third,

        [global::System.Runtime.Serialization.EnumMember(Value = "Custom Name")]
        CustomName = 1 << 3,
    }
}
