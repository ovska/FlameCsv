using FlameCsv.Converters;

namespace FlameCsv.Tests.Converters;

public class EnumNoCacheCharTests : EnumTests<char>
{
    protected override CsvConverter<char, DayOfWeek> GetDayOfWeek(bool numeric, bool ignoreCase)
    {
        return new EnumTextConverter<DayOfWeek>(ignoreCase, numeric ? "D" : "G");
    }

    protected override CsvConverter<char, Animal> GetAnimal(bool numeric, bool ignoreCase)
    {
        return new EnumTextConverter<Animal>(ignoreCase, numeric ? "D" : "G");
    }

    protected override CsvConverter<char, Negatives> GetNegatives(bool numeric, bool ignoreCase)
    {
        return new EnumTextConverter<Negatives>(ignoreCase, numeric ? "D" : "G");
    }

    protected override CsvConverter<char, NotAscii> GetNotAscii(bool numeric, bool ignoreCase)
    {
        return new EnumTextConverter<NotAscii>(ignoreCase, numeric ? "D" : "G");
    }
}
