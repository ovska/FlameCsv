using FlameCsv.Converters;

namespace FlameCsv.Tests.Converters;

public class EnumCacheByteTests : EnumTests<byte>
{
    protected override CsvConverter<byte, DayOfWeek> GetDayOfWeek(bool numeric, bool ignoreCase)
    {
        return new EnumUtf8Converter<DayOfWeek>(GetOpts(numeric, ignoreCase));
    }

    protected override CsvConverter<byte, Animal> GetAnimal(bool numeric, bool ignoreCase)
    {
        return new EnumUtf8Converter<Animal>(GetOpts(numeric, ignoreCase));
    }

    protected override CsvConverter<byte, Negatives> GetNegatives(bool numeric, bool ignoreCase)
    {
        return new EnumUtf8Converter<Negatives>(GetOpts(numeric, ignoreCase));
    }

    protected override CsvConverter<byte, NotAscii> GetNotAscii(bool numeric, bool ignoreCase)
    {
        return new EnumUtf8Converter<NotAscii>(GetOpts(numeric, ignoreCase));
    }
}
