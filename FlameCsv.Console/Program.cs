using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;
using FlameCsv.Converters;

#pragma warning disable IDE0161 // Convert to file-scoped namespace
namespace FlameCsv.Console
{
    public static class Program
    {
        static void Main([NotNull] string[] args)
        {
            var test = CsvReader.Read<Obj>(
                "id,name,is_enabled\r\n1,Bob,true", new ObjTypeMap(), CsvTextOptions.Default).ToList();
            _ = 1;
        }

        static void Test(bool a, bool b, bool c)
        {
            _ = a || b && c;
        }
    }


    [CsvTypeMap<char, Obj>(ThrowOnDuplicate = false, IgnoreUnmatched = false)]
    partial class ObjTypeMap
    {
    }

    public class Obj
    {
        [CsvConstructor]
        public Obj(
            long position = 0,
            [CsvHeader(Order = -1, Required = true)] in int id = -1)
        {
            Id = id;
            Position = position;
        }


        public long Position { get; }

        public int Id { get; }

        [CsvHeader(Required = true)]
        [CsvConverter<char, StringTextConverter>]
        public string Name { get; init; } = "";

        [CsvHeader("isenabled", "is_enabled", Order = 5)] public bool IsEnabled { get; set; }
    }

    [StructLayoutAttribute(LayoutKind.Sequential)]
    public struct StructWithoutReferences
    {
        public int a, b, c;
    }

    [StructLayoutAttribute(LayoutKind.Sequential)]
    public struct StructWithReferences
    {
        public int a, b, c;
        public object d;
    }

    public struct StructWithReferences2
    {
        public int a, b, c;
        public object d { get; }
    }
}

