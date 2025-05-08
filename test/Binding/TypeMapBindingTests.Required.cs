#if false
using FlameCsv.Attributes;
using FlameCsv.Converters.Enums;

// ReSharper disable all

namespace FlameCsv.Tests.Binding;

public static partial class TypeMapBindingTests
{
    [CsvHeader("__id__", MemberName = "Id")]
    [CsvHeader("doeeeef", IsParameter = true, MemberName = "dof")]
    [CsvHeader("aaaaasd", MemberName = "Xyzz")]
    [CsvRequired(MemberName = "Xyzz")]
    [CsvConstructor(ParameterTypes = [typeof(string), typeof(DayOfWeek), typeof(DayOfWeek?)])]
    private class _Obj : ISomething
    {
        public _Obj(string? name = "\\test", DayOfWeek dof = DayOfWeek.Sunday, DayOfWeek? dof2 = DayOfWeek.Wednesday)
        {
            Name = name;
        }

        public _Obj(int i)
        {
        }

        public int Id { get; set; }
        public string? Name { get; set; }

        [CsvConverter<EnumTextConverterFactory>]
        public DayOfWeek DOF { get; set; }

        [CsvRequired] public int? NullableInt { get; set; }
        public DayOfWeek? NullableDOF { get; set; }

        bool ISomething.Xyzz { get; set; }

        [CsvIgnore] public long SomeValue { get; set; }
    }

    [CsvTypeMap<char, _Obj>]
    private partial class Test;

    private interface ISomething
    {
        bool Xyzz { get; set; }
    }
}
#endif
