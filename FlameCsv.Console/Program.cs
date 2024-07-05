using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;
using FlameCsv.Converters;

namespace FlameCsv.Console
{
    public static class Program
    {
        static void Main([NotNull] string[] args)
        {
            var dst = new byte[128];
            var res = Utf8Formatter.TryFormat((Int16)0, dst, out int written);

            var test = CsvReader.Read<Obj>("id,name,isenabled\r\n1,Bob,true", ObjTypeMap.Instance).ToList();

            System.Console.WriteLine(test[0].Id);
            System.Console.WriteLine(test[0].Name);
            System.Console.WriteLine(test[0].IsEnabled);
        }
    }

    [CsvTypeMap<char, Obj>(ThrowOnDuplicate = false, IgnoreUnmatched = false)]
    partial class ObjTypeMap;

    public class Obj
    {
        public DayOfWeek DOF { get; set; }
        public int Id { get; set; }
        public string? Name { get; set; }
        public bool IsEnabled { get; set; }
        [CsvConverter<char, SpanTextConverter<long>>] public long? Age { get; set; }
    }
}
