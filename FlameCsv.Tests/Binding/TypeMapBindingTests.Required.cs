using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;
using FlameCsv.Converters;

// ReSharper disable all

namespace FlameCsv.Tests.Binding;

public static partial class TypeMapBindingTests
{
    [CsvTypeField(memberName: "Id", "__id__")]
    [CsvTypeField(memberName: "dof", "doeeeef", IsParameter = true)]
    [CsvTypeField(memberName: "Xyzz", "aaaaasd", IsRequired = false)]
    private class _Obj : ISomething
    {
        [CsvConstructor]
        public _Obj(string? name = "\\test", DayOfWeek dof = DayOfWeek.Sunday, DayOfWeek? dof2 = DayOfWeek.Wednesday)
        {
            Name = name;
        }

        public int Id { get; set; }
        public string? Name { get; set; }

        [CsvConverter<char, EnumTextConverterFactory>]
        public DayOfWeek DOF { get; set; }

        [CsvField(IsRequired = true)]
        public int? NullableInt { get; set; }
        public DayOfWeek? NullableDOF { get; set; }

        bool ISomething.Xyzz { get; set; }

        [CsvField(IsIgnored = true)]
        public long SomeValue { get; set; }
    }

    [CsvTypeMap<char, _Obj>]
    private partial class Test;

    private interface ISomething
    {
        bool Xyzz { get; set; }
    }
}
