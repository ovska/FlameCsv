using System.Diagnostics.CodeAnalysis;
using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;
using FlameCsv.Parsers.Text;

#pragma warning disable IDE0161 // Convert to file-scoped namespace
namespace FlameCsv.Console
{
    public static class Program
    {
        static void Main([NotNull] string[] args)
        {
            var test = CsvReader.Read<Obj>(
                "id,name,is_enabled\r\n1,Bob,true", new ObjTypeMap(), CsvTextReaderOptions.Default).ToList();
            _ = 1;
        }

        static void Test(bool a, bool b, bool c)
        {
            _ = a || b && c;
        }
    }

    [CsvTypeMap<char, Obj>(ThrowOnDuplicate = false)]
    partial class ObjTypeMap
    {
    }

    public class Obj
    {
        [CsvHeader(Order = -1)] public int Id { get; set; }

        [CsvHeaderRequired]
        [CsvParserOverride<char, StringTextParser>]
        public string? Name { get; set; }

        [CsvHeader("isenabled", "is_enabled", Order = 5)] public bool IsEnabled { get; set; }
    }
}

