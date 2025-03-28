using FlameCsv.Converters;

namespace FlameCsv.Tests.Converters;

public class EnumNoCacheByteTests : EnumTests<byte>
{
    protected override CsvConverter<byte, DayOfWeek> GetDayOfWeek(bool numeric, bool ignoreCase)
    {
        return new EnumUtf8Converter<DayOfWeek>(ignoreCase, numeric ? "D" : "G");
    }

    protected override CsvConverter<byte, Animal> GetAnimal(bool numeric, bool ignoreCase)
    {
        return new EnumUtf8Converter<Animal>(ignoreCase, numeric ? "D" : "G");
    }

    protected override CsvConverter<byte, Negatives> GetNegatives(bool numeric, bool ignoreCase)
    {
        return new EnumUtf8Converter<Negatives>(ignoreCase, numeric ? "D" : "G");
    }

    protected override CsvConverter<byte, NotAscii> GetNotAscii(bool numeric, bool ignoreCase)
    {
        return new EnumUtf8Converter<NotAscii>(ignoreCase, numeric ? "D" : "G");
    }
}
