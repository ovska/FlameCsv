using FlameCsv.Attributes;

namespace FlameCsv.Tests.Converters;

public partial class EnumGeneratorByteTests : EnumTests<byte>
{
    protected override CsvConverter<byte, DayOfWeek> GetDayOfWeek(bool numeric, bool ignoreCase)
    {
        return new DayOfWeekConverter(GetOpts(numeric, ignoreCase));
    }

    protected override CsvConverter<byte, Animal> GetAnimal(bool numeric, bool ignoreCase)
    {
        return new AnimalConverter(GetOpts(numeric, ignoreCase));
    }

    protected override CsvConverter<byte, Negatives> GetNegatives(bool numeric, bool ignoreCase)
    {
        return new NegativeConverter(GetOpts(numeric, ignoreCase));
    }

    protected override CsvConverter<byte, NotAscii> GetNotAscii(bool numeric, bool ignoreCase)
    {
        return new NotAsciiConverter(GetOpts(numeric, ignoreCase));
    }

    [CsvEnumConverter<byte, DayOfWeek>]
    private partial class DayOfWeekConverter;

    [CsvEnumConverter<byte, Animal>]
    private partial class AnimalConverter;

    [CsvEnumConverter<byte, Negatives>]
    private partial class NegativeConverter;

    [CsvEnumConverter<byte, NotAscii>]
    private partial class NotAsciiConverter;
}
