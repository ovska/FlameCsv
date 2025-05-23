﻿using FlameCsv.Attributes;
using FlameCsv.Converters;
using FlameCsv.Converters.Formattable;

// ReSharper disable InconsistentNaming

namespace FlameCsv.Tests.Converters;

public static partial class ConverterResolutionTests
{
    private const string Data = "id,name,age\r\n5,,0";

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
        [CsvConverter<IdConverter>]
        public int Id { get; set; }

        [CsvConverter<PoolingStringTextConverter>]
        public string? Name { get; set; }

        [CsvConverter<IdConverter>]
        public int? Age { get; set; }
    }

    private class IdConverter : CsvConverter<char, int>
    {
        public override bool TryParse(ReadOnlySpan<char> source, out int value)
        {
            value = 123;
            return true;
        }

        public override bool TryFormat(Span<char> destination, int value, out int charsWritten) =>
            throw new NotImplementedException();
    }

    [Fact]
    public static void Should_Resolve_Correct_From_Attribute()
    {
        var o1 = new CsvOptions<char>();
        var o2 = new CsvOptions<char>();

        var r1 = CsvReader.Read<ShimOR>(Data, o1).ToList();
        var r2 = CsvReader.Read(Data, TypeMapOR.Default, o2).ToList();

        Assert.Equal(123, r1[0].Id);
        Assert.Equal(123, r2[0].Id);
        Assert.Equal(123, r1[0].Age);
        Assert.Equal(123, r2[0].Age);

        // overridden converter should not have any effect on configured one
        Assert.IsType<NumberTextConverter<int>>(o1.GetConverter<int>());
        Assert.IsType<NumberTextConverter<int>>(o2.GetConverter<int>());
        Assert.IsType<StringTextConverter>(o1.GetConverter<string>());
        Assert.IsType<StringTextConverter>(o2.GetConverter<string>());
    }

    [Fact]
    public static void Should_Resolve_Correct_Explicitly()
    {
        var o1 = new CsvOptions<char> { Converters = { new IdConverter() } };
        var o2 = new CsvOptions<char> { Converters = { new IdConverter() } };

        var r1 = CsvReader.Read<ShimNR>(Data, o1).ToList();
        var r2 = CsvReader.Read(Data, TypeMapNR.Default, o2).ToList();

        Assert.Equal(123, r1[0].Id);
        Assert.Equal(123, r2[0].Id);

        Assert.IsType<IdConverter>(o1.GetConverter<int>());
        Assert.IsType<IdConverter>(o2.GetConverter<int>());
    }
}
