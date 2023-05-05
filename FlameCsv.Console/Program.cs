using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;
using FlameCsv.Parsers;
using FlameCsv.Parsers.Text;
using FlameCsv.Reading;
using FlameCsv.Runtime;

#pragma warning disable IDE0161 // Convert to file-scoped namespace
namespace FlameCsv.Console
{
    public static class Program
    {
        static void Main(string[] args)
        {
            var test = CsvReader.Read<Obj>(
                "id,name,isenabled\r\n1,Bob,true", new ObjTypeMap(), CsvTextReaderOptions.Default).ToList();
            _ = 1;
        }
    } 

     
    [CsvTypeMap<Obj>(IgnoreUnparsable = true)]
    partial class ObjTypeMap
    {
    }

    public class Obj
    {
        public int Id { get; set; }

        [CsvHeaderRequired]
        public string? Name { get; set; }

        [CsvHeader("isenabled", "is_enabled")] public bool IsEnabled { get; set; }
    }
}

