using FlameCsv.Converters.Enums;

namespace FlameCsv.Tests.Converters;

public class EnumReflectionCharTests : EnumTests<char>
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

    protected override CsvConverter<char, FlagsEnum> GetFlagsEnum(bool numeric, bool ignoreCase, bool allowUndefined)
    {
        var opts = GetOpts(numeric, ignoreCase);
        opts.AllowUndefinedEnumValues = allowUndefined;
        opts.EnumFormat = numeric ? "D" : "F";
        return new EnumTextConverter<FlagsEnum>(opts);
    }
}
