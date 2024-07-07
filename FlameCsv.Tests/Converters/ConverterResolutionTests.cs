using System.Diagnostics.CodeAnalysis;
using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;
using FlameCsv.Converters;

namespace FlameCsv.Tests.Converters;

public static partial class ConverterResolutionTests
{
    private const string data = "id,name,age\r\n5,,0";

    [CsvTypeMap<char, ShimNR>]
    private partial class TypeMapNR;

    [CsvTypeMap<char, ShimOR>]
    private partial class TypeMapOR;

    private class ShimNR
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int Age { get; set; }
    }

    private class ShimOR
    {
        [CsvConverter<char, IdConverter>] public int Id { get; set; }
        [CsvConverter<char, PoolingStringTextConverter>] public string? Name { get; set; }
        [CsvConverter<char, IdConverter>] public int? Age { get; set; }
    }

    private class IdConverter : CsvConverter<char, int>
    {
        public override bool TryParse(ReadOnlySpan<char> source, [MaybeNullWhen(false)] out int value)
        {
            value = 123;
            return true;
        }

        public override bool TryFormat(Span<char> destination, int value, out int charsWritten) => throw new NotImplementedException();
    }

    [Fact]
    public static void Should_Resolve_Correct_From_Attribute()
    {
        var o1 = new CsvOptions<char>();
        var o2 = new CsvOptions<char>();

        var r1 = CsvReader.Read<ShimOR>(data, o1).ToList();
        var r2 = CsvReader.Read<ShimOR>(data, TypeMapOR.Instance, o2).ToList();

        Assert.Equal(123, r1[0].Id);
        Assert.Equal(123, r2[0].Id);
        Assert.Equal(123, r1[0].Age);
        Assert.Equal(123, r2[0].Age);

        // overridden converter should not have any effect on configured one
        Assert.IsType<IntegerNumberTextConverter<int>>(o1.GetConverter<int>());
        Assert.IsType<IntegerNumberTextConverter<int>>(o2.GetConverter<int>());
        Assert.IsType<StringTextConverter>(o1.GetConverter<string>());
        Assert.IsType<StringTextConverter>(o2.GetConverter<string>());
    }

    [Fact]
    public static void Should_Resolve_Correct_Explicitly()
    {
        var o1 = new CsvOptions<char> { Converters = { new IdConverter() } };
        var o2 = new CsvOptions<char> { Converters = { new IdConverter() } };

        var r1 = CsvReader.Read<ShimNR>(data, o1).ToList();
        var r2 = CsvReader.Read<ShimNR>(data, TypeMapNR.Instance, o2).ToList();

        Assert.Equal(123, r1[0].Id);
        Assert.Equal(123, r2[0].Id);

        Assert.IsType<IdConverter>(o1.GetConverter<int>());
        Assert.IsType<IdConverter>(o2.GetConverter<int>());
    }
}
