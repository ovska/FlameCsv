using System.Buffers;
using FlameCsv.Binding.Attributes;

namespace FlameCsv.Tests.TestData;

internal class Obj
{
    [IndexBinding(0)] public int Id { get; set; }
    [IndexBinding(1)] public string? Name { get; set; }
    [IndexBinding(2)] public bool IsEnabled { get; set; }
    [IndexBinding(3)] public DateTimeOffset LastLogin { get; set; }
    [IndexBinding(4)] public Guid Token { get; set; }
}

internal static class TestDataGenerator
{
    internal static readonly byte[] _guidbytes = { 0, 1, 2, 3, 4, 5, 6, 7 };

    public static void Generate(
        IBufferWriter<char> writer,
        string newLine,
        bool writeHeader,
        bool writeTrailingNewline,
        bool requireEscaping,
        bool hasWhitespace)
    {
        if (writeHeader)
        {
            writer.Write("Id,Name,IsEnabled,LastLogin,Token");
            writer.Write(newLine);
        }

        for (int i = 0; i < 1_000; i++)
        {
            if (i != 0) writer.Write(newLine);

            writer.Write(requireEscaping ? $"\"{i}\"" : i.ToString());
            writer.Write(",");
            if (hasWhitespace) writer.Write(" ");
            writer.Write(requireEscaping ? $"\"Name\"\"{i}\"" : $"Name-{i}");
            if (hasWhitespace) writer.Write(" ");
            writer.Write(",");
            if (hasWhitespace) writer.Write(" ");
            writer.Write(i % 2 == 0 ? "true" : "false");
            if (hasWhitespace) writer.Write(" ");
            writer.Write(",");
            writer.Write(DateTimeOffset.UnixEpoch.AddDays(i).ToString("O"));
            writer.Write(",");
            writer.Write(new Guid(i, 0, 0, _guidbytes).ToString());
        }

        if (writeTrailingNewline) writer.Write(newLine);
    }
}
