using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;
using FlameCsv.Converters;
using FlameCsv.Reading;

#pragma warning disable IDE0161 // Convert to file-scoped namespace
namespace FlameCsv.Console
{
    public static class Program
    {
        static void Main([NotNull] string[] args)
        {
            var s_c_b = Unsafe.SizeOf<CsvReadingContext<byte>>();
            var s_c_c = Unsafe.SizeOf<CsvReadingContext<char>>();
            var s_r_b = Unsafe.SizeOf<CsvFieldReader<byte>>();
            var s_r_c = Unsafe.SizeOf<CsvFieldReader<char>>();

            Span<char> src = stackalloc char[] { 'x', 'y', 'z', '_' };
            Span<char> dst = stackalloc char[] { 'a','b','c','d' };

            ref byte srcRef = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(src));
            ref byte dstRef = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(dst));

            Unsafe.CopyBlockUnaligned(ref dstRef, ref srcRef, (uint)Unsafe.SizeOf<char>() * (uint)dst.Length / (uint)Unsafe.SizeOf<byte>());



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

