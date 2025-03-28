using FlameCsv.Converters;

namespace FlameCsv.Tests.Converters;

public class EnumCacheCharTests : EnumTests<char>
{
    protected override CsvConverter<char, DayOfWeek> GetDayOfWeek(bool numeric, bool ignoreCase)
    {
        return new EnumTextConverter<DayOfWeek>(GetOpts(numeric, ignoreCase));
    }

    protected override CsvConverter<char, Animal> GetAnimal(bool numeric, bool ignoreCase)
    {
        return new EnumTextConverter<Animal>(GetOpts(numeric, ignoreCase));
    }

    protected override CsvConverter<char, Negatives> GetNegatives(bool numeric, bool ignoreCase)
    {
        return new EnumTextConverter<Negatives>(GetOpts(numeric, ignoreCase));
    }

    protected override CsvConverter<char, NotAscii> GetNotAscii(bool numeric, bool ignoreCase)
    {
        return new EnumTextConverter<NotAscii>(GetOpts(numeric, ignoreCase));
    }
}
