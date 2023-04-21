using System.Buffers;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Binding.Attributes;

namespace FlameCsv.Tests.TestData;

internal class Obj
{
    [CsvIndex(0)] public int Id { get; set; }
    [CsvIndex(1)] public string? Name { get; set; }
    [CsvIndex(2)] public bool IsEnabled { get; set; }
    [CsvIndex(3)] public DateTimeOffset LastLogin { get; set; }
    [CsvIndex(4)] public Guid Token { get; set; }
}

public enum EscapeArg
{
    None = 0, Quotes = 1, QuotesAndEscapes = 2,
}

internal static class TestDataGenerator
{
    public const string Header = "Id,Name,IsEnabled,LastLogin,Token";

    internal static readonly byte[] _guidbytes = { 0, 1, 2, 3, 4, 5, 6, 7 };

    public static void Generate(
        ArrayPoolBufferWriter<char> writer,
        string newLine,
        bool writeHeader,
        bool writeTrailingNewline,
        EscapeArg escaping)
    {
        if (writeHeader)
        {
            writer.Write(Header);
            writer.Write(newLine);
        }

        for (int i = 0; i < 1_000; i++)
        {
            if (i != 0) writer.Write(newLine);

            writer.Write(escaping != EscapeArg.None ? $"\"{i}\"" : i.ToString());
            writer.Write(",");
            writer.Write(escaping switch
            {
                EscapeArg.QuotesAndEscapes => $"\"Name^\"{i}\"",
                EscapeArg.Quotes => $"\"Name\"\"{i}\"",
                _ => $"Name-{i}",
            });
            writer.Write(",");
            writer.Write(i % 2 == 0 ? "true" : "false");
            writer.Write(",");
            writer.Write(DateTimeOffset.UnixEpoch.AddDays(i).ToString("O"));
            writer.Write(",");
            writer.Write(new Guid(i, 0, 0, _guidbytes).ToString());
        }

        if (writeTrailingNewline) writer.Write(newLine);


    }
}
