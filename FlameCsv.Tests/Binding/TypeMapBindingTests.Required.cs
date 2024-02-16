using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;

namespace FlameCsv.Tests.Binding;

public static partial class TypeMapBindingTests
{
    [CsvHeaderTarget("Id", "__id__")]
    private class _Obj : ISomething
    {
        [CsvConstructor]
        public _Obj(string? name = "\\test", DayOfWeek dof = DayOfWeek.Tuesday, DayOfWeek? dof2 = DayOfWeek.Wednesday)
        {
            Name = name;
        }

        public int Id { get; set; }
        public string? Name { get; set; }
        public DayOfWeek DOF { get; set; }
        public int? NullableInt { get; set; }
        public DayOfWeek? NullableDOF { get; set; }
        bool ISomething.Xyzz { get; set; }
    }

    [CsvTypeMap<char, _Obj>()]
    private partial class Test
    {
    }

    private interface ISomething
    {
        bool Xyzz { get; set; }
    }
}
