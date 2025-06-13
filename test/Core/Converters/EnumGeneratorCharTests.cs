using FlameCsv.Attributes;

namespace FlameCsv.Tests.Converters;

public partial class EnumGeneratorCharTests : EnumTests<char>
{
    protected override CsvConverter<char, DayOfWeek> GetDayOfWeek(bool numeric, bool ignoreCase)
    {
        return new DayOfWeekConverter(GetOpts(numeric, ignoreCase));
    }

    protected override CsvConverter<char, Animal> GetAnimal(bool numeric, bool ignoreCase)
    {
        return new AnimalConverter(GetOpts(numeric, ignoreCase));
    }

    protected override CsvConverter<char, Negatives> GetNegatives(bool numeric, bool ignoreCase)
    {
        return new NegativeConverter(GetOpts(numeric, ignoreCase));
    }

    protected override CsvConverter<char, NotAscii> GetNotAscii(bool numeric, bool ignoreCase)
    {
        return new NotAsciiConverter(GetOpts(numeric, ignoreCase));
    }

    protected override CsvConverter<char, UnconventionalNames> GetUnconventionalNames(bool numeric, bool ignoreCase)
    {
        return new UnconventionalNamesConverter(GetOpts(numeric, ignoreCase));
    }

    protected override CsvConverter<char, FlagsEnum> GetFlagsEnum(bool numeric, bool ignoreCase, bool allowUndefined)
    {
        var opts = GetOpts(numeric, ignoreCase);
        opts.AllowUndefinedEnumValues = allowUndefined;
        opts.EnumFormat = numeric ? "D" : "F";
        return new FlagsEnumConverter(opts);
    }

    [CsvEnumConverter<char, DayOfWeek>]
    private partial class DayOfWeekConverter;

    [CsvEnumConverter<char, Animal>]
    private partial class AnimalConverter;

    [CsvEnumConverter<char, Negatives>]
    private partial class NegativeConverter;

    [CsvEnumConverter<char, NotAscii>]
    private partial class NotAsciiConverter;

    [CsvEnumConverter<char, FlagsEnum>]
    private partial class FlagsEnumConverter;

    [CsvEnumConverter<char, UnconventionalNames>]
    private partial class UnconventionalNamesConverter;
}
