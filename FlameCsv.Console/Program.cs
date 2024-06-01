using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;
using FlameCsv.Converters;
using FlameCsv.Reading;
using FlameCsv.Writing;

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

            var options = CsvTextOptions.Default;
            CsvConverter<char, long?> @__Converter_Age = FlameCsv.Converters.DefaultConverters.GetOrCreate(options, static o => new NullableConverter<char, long>(FlameCsv.Converters.DefaultConverters.CreateInt64((FlameCsv.CsvTextOptions)o), o.GetNullToken(typeof(long))));
        }
    }

    [CsvTypeMap<char, Obj>(ThrowOnDuplicate = false, IgnoreUnmatched = false)]
    partial class ObjTypeMap;

    public class Obj
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public bool IsEnabled { get; set; }
        [CsvConverter<char, Int64TextConverter>] public long? Age { get; set; }
    }
}
