using FlameCsv.Attributes;

namespace FlameCsv.Tests.Converters;

public class EnumGeneratorTests
{
    static void Test(ReadOnlySpan<char> source)
    {
    }
}

[CsvEnumConverter<char, TestEnum>]
partial class EnumConverter;

enum TestEnum
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
