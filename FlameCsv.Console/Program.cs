﻿// ReSharper disable all

using System.Buffers;
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
            var c0 = SearchValues.Create("\"");
            var c1 = SearchValues.Create(",\"");
            var c2 = SearchValues.Create("^\"");
            var c3 = SearchValues.Create("^\",");
            var b0 = SearchValues.Create("\""u8);
            var b1 = SearchValues.Create(",\""u8);
            var b2 = SearchValues.Create("^\""u8);
            var b3 = SearchValues.Create("^\","u8);

            object? x = (object)1;

            var dof = (DayOfWeek)x;

            var dst = new byte[128];
            var res = Utf8Formatter.TryFormat((Int16)0, dst, out int written);

            var test = CsvReader.Read<Obj>("id,name,isenabled\r\n1,Bob,true", ObjTypeMap.Instance).ToList();

            System.Console.WriteLine(test[0].Id);
            System.Console.WriteLine(test[0].Name);
            System.Console.WriteLine(test[0].IsEnabled);
        }
    }

    [CsvTypeMap<char, Obj>(ThrowOnDuplicate = false, IgnoreUnmatched = true)]
    partial class ObjTypeMap;

    public class Obj
    {
        public DayOfWeek DOF { get; set; }
        public int Id { get; set; }
        public string? Name { get; set; }
        [CsvHeader("Enabled")] public bool IsEnabled { get; set; }
        [CsvConverter<char, SpanTextConverter<long>>] public long? Age { get; set; }
    }

    class X : Base
    {
        protected override int Id => 123;
    }

    abstract class Base
    {
        protected abstract int Id { get; }

        public Base()
        {
            System.Console.WriteLine(Id);
        }
    }
}
