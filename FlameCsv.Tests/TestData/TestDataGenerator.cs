using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;
using FlameCsv.Tests.Readers;
using Utf8StringInterpolation;
using U8 = Utf8StringInterpolation.Utf8String;

namespace FlameCsv.Tests.TestData;

[CsvTypeMap<char, Obj>] public partial class ObjCharTypeMap;
[CsvTypeMap<byte, Obj>] public partial class ObjByteTypeMap;

public class Obj : IEquatable<Obj>
{
    [CsvIndex(0)] public int Id { get; set; }
    [CsvIndex(1)] public string? Name { get; set; }
    [CsvIndex(2)] public bool IsEnabled { get; set; }
    [CsvIndex(3)] public DateTimeOffset LastLogin { get; set; }
    [CsvIndex(4)] public Guid Token { get; set; }

    public bool Equals(Obj? other)
    {
        return other is not null
            && Id.Equals(other.Id)
            && StringComparer.Ordinal.Equals(Name, other.Name)
            && IsEnabled.Equals(other.IsEnabled)
            && LastLogin.Equals(other.LastLogin)
            && Token.Equals(other.Token);
    }
}

public enum Mode
{
    None = 0, RFC = 1, Escape = 2,
}

internal static class TestDataGenerator
{
    public const string Header = "Id,Name,IsEnabled,LastLogin,Token";
    public const string HeaderQuoted = "'Id','Name','IsEnabled','LastLogin','Token'";
    public static ReadOnlySpan<byte> HeaderU8 => "Id,Name,IsEnabled,LastLogin,Token"u8;
    public static ReadOnlySpan<byte> HeaderQuotedU8 => "'Id','Name','IsEnabled','LastLogin','Token'"u8;

    internal static readonly byte[] _guidbytes = [0, 1, 2, 3, 4, 5, 6, 7];

    private static readonly ConcurrentDictionary<Key, Lazy<ReadOnlyMemory<char>>> _chars = [];
    private static readonly ConcurrentDictionary<Key, Lazy<ArraySegment<byte>>> _bytes = [];

    private readonly record struct Key(
        string Newline,
        bool WriteHeader,
        bool TrailingNewline,
        Mode Escaping);

    public static ReadOnlyMemory<T> Generate<T>(
        NewlineToken newLineToken,
        bool writeHeader,
        bool writeTrailingNewline,
        Mode escaping)
    {
        if (typeof(T) == typeof(char))
            return (ReadOnlyMemory<T>)(object)GenerateText(newLineToken, writeHeader, writeTrailingNewline, escaping);

        if (typeof(T) == typeof(byte))
            return (ReadOnlyMemory<T>)(object)GenerateBytes(newLineToken, writeHeader, writeTrailingNewline, escaping);

        throw new UnreachableException();
    }

    public static ReadOnlyMemory<char> GenerateText(
        NewlineToken newLineToken,
        bool writeHeader,
        bool writeTrailingNewline,
        Mode escaping)
    {
        string newLine = newLineToken switch
        {
            NewlineToken.LF or NewlineToken.AutoLF => "\n",
            _ => "\r\n",
        };
        var key = new Key(newLine, writeHeader, writeTrailingNewline, escaping);
        var chars = _chars.GetOrAdd(
            key,
            static key => new Lazy<ReadOnlyMemory<char>>(() =>
            {
                var (newLine, writeHeader, writeTrailingNewline, escaping) = key;

                var writer = new StringBuilder(capacity: 131_072);

                if (writeHeader)
                {
                    writer.Append(Header);
                    writer.Append(newLine);
                }

                for (int i = 0; i < 1_000; i++)
                {
                    if (i != 0)
                        writer.Append(newLine);

                    if (escaping != Mode.None)
                    {
                        writer.Append($"\"{i}\"");
                    }
                    else
                    {
                        writer.Append(i);
                    }

                    writer.Append(',');

                    if (escaping == Mode.Escape)
                    {
                        writer.Append($"\"Name^\"{i}\"");
                    }
                    else if (escaping == Mode.RFC)
                    {
                        writer.Append($"\"Name\"\"{i}\"");
                    }
                    else
                    {
                        writer.Append($"Name-{i}");
                    }

                    writer.Append(',');
                    writer.Append(i % 2 == 0 ? "true" : "false");
                    writer.Append(',');
                    writer.Append($"{DateTimeOffset.UnixEpoch.AddDays(i):O}");
                    writer.Append(',');
                    writer.Append($"{new Guid(i, 0, 0, _guidbytes)}");
                }

                if (writeTrailingNewline)
                    writer.Append(newLine);

                return writer.ToString().AsMemory();
            }));

        return chars.Value;
    }

    public static ReadOnlyMemory<byte> GenerateBytes(
        NewlineToken newLineToken,
        bool writeHeader,
        bool writeTrailingNewline,
        Mode escaping)
    {
        string newLine = newLineToken switch
        {
            NewlineToken.LF or NewlineToken.AutoLF => "\n",
            _ => "\r\n",
        };
        var key = new Key(newLine, writeHeader, writeTrailingNewline, escaping);
        var chars = _bytes.GetOrAdd(
            key,
            static key => new Lazy<ArraySegment<byte>>(() =>
            {
                var (newLine, writeHeader, writeTrailingNewline, escaping) = key;

                using var apbf = new ArrayPoolBufferWriter<byte>(initialCapacity: 131_072);
                var writer = U8.CreateWriter(apbf);

                if (writeHeader)
                {
                    writer.Append(Header);
                    writer.Append(newLine);
                }

                for (int i = 0; i < 1_000; i++)
                {
                    if (i != 0)
                        writer.Append(newLine);

                    if (escaping != Mode.None)
                    {
                        writer.AppendFormat($"\"{i}\"");
                    }
                    else
                    {
                        writer.AppendFormatted(i);
                    }

                    writer.Append(',');

                    if (escaping == Mode.Escape)
                    {
                        writer.AppendFormat($"\"Name^\"{i}\"");
                    }
                    else if (escaping == Mode.RFC)
                    {
                        writer.AppendFormat($"\"Name\"\"{i}\"");
                    }
                    else
                    {
                        writer.AppendFormat($"Name-{i}");
                    }

                    writer.Append(',');
                    writer.Append(i % 2 == 0 ? "true" : "false");
                    writer.Append(',');
                    writer.AppendFormatted(DateTimeOffset.UnixEpoch.AddDays(i), format: "O");
                    writer.Append(',');
                    writer.AppendFormatted(new Guid(i, 0, 0, _guidbytes));
                }

                if (writeTrailingNewline)
                    writer.Append(newLine);

                writer.Dispose();
                return apbf.WrittenMemory.ToArray();
            }));

        return chars.Value;
    }

    public static readonly Lazy<List<Obj>> Objects = new(() =>
    {
        var list = new List<Obj>(1000);

        for (int i = 0; i < 1000; i++)
        {
            list.Add(new Obj
            {
                Id = i,
                IsEnabled = i % 2 == 0,
                LastLogin = DateTimeOffset.UnixEpoch,
                Token = new Guid(i, 0, 0, _guidbytes),
                Name = $" Name'{i}",
            });
        }

        return list;
    }, LazyThreadSafetyMode.ExecutionAndPublication);
}
