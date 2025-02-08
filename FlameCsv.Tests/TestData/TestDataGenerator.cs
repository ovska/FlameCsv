using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using FlameCsv.Attributes;
using FlameCsv.Binding;
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
    private const int RequiredCapacity = 131_072;

    public const string Header = "Id,Name,IsEnabled,LastLogin,Token";
    public const string HeaderQuoted = "'Id','Name','IsEnabled','LastLogin','Token'";
    public static ReadOnlySpan<byte> HeaderU8 => "Id,Name,IsEnabled,LastLogin,Token"u8;
    public static ReadOnlySpan<byte> HeaderQuotedU8 => "'Id','Name','IsEnabled','LastLogin','Token'"u8;

    internal static readonly byte[] GuidBytes = [0, 1, 2, 3, 4, 5, 6, 7];

    private static readonly ConcurrentDictionary<Key, Lazy<ReadOnlyMemory<char>>> _chars = [];
    private static readonly ConcurrentDictionary<Key, Lazy<ReadOnlyMemory<byte>>> _bytes = [];

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

                var writer = new StringBuilder(capacity: RequiredCapacity);

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
                    writer.Append($"{new Guid(i, 0, 0, GuidBytes)}");
                }

                if (writeTrailingNewline)
                    writer.Append(newLine);

                if (writer.Capacity != RequiredCapacity)
                    throw new UnreachableException(writer.Capacity.ToString());

                var enumerator = writer.GetChunks();

                if (enumerator.MoveNext())
                {
                    var result = enumerator.Current;

                    if (!enumerator.MoveNext())
                    {
                        return result;
                    }
                }

                throw new UnreachableException();
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
            static key => new Lazy<ReadOnlyMemory<byte>>(() =>
            {
                var (newLine, writeHeader, writeTrailingNewline, escaping) = key;

                var innerWriter = new ArrayBufferWriter<byte>(initialCapacity: RequiredCapacity);
                var writer = U8.CreateWriter(innerWriter);

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
                    writer.AppendFormatted(new Guid(i, 0, 0, GuidBytes));
                }

                if (writeTrailingNewline)
                    writer.Append(newLine);

                if (innerWriter.Capacity != RequiredCapacity)
                    throw new UnreachableException(innerWriter.Capacity.ToString());

                writer.Dispose();
                return innerWriter.WrittenMemory;
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
                Token = new Guid(i, 0, 0, GuidBytes),
                Name = $" Name'{i}",
            });
        }

        return list;
    }, LazyThreadSafetyMode.ExecutionAndPublication);
}
