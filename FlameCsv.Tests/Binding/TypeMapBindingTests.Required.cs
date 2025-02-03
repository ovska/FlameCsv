using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;
using FlameCsv.Converters;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedParameter.Local
// ReSharper disable IdentifierTypo

namespace FlameCsv.Tests.Binding;

public static partial class TypeMapBindingTests
{
    [CsvTypeField(memberName: "Id", "__id__")]
    [CsvTypeField(memberName: "dof", "doeeeef", IsParameter = true)]
    private class _Obj : ISomething
    {
        [CsvConstructor]
        public _Obj(string? name = "\\test", DayOfWeek dof = DayOfWeek.Tuesday, DayOfWeek? dof2 = DayOfWeek.Wednesday)
        {
            Name = name;
        }

        public int Id { get; set; }
        public string? Name { get; set; }

        [CsvConverter<char, EnumTextConverter<DayOfWeek>>]
        public DayOfWeek DOF { get; set; }

        public int? NullableInt { get; set; }
        public DayOfWeek? NullableDOF { get; set; }

        [CsvField(IsRequired = false)]
        bool ISomething.Xyzz { get; set; }
    }

    [CsvTypeMap<char, _Obj>]
    private partial class Test;

    private interface ISomething
    {
        bool Xyzz { get; set; }
    }
}
