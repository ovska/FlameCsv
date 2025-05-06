using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using FlameCsv.Attributes;
using FlameCsv.Utilities;
using Utf8StringInterpolation;

namespace FlameCsv.Tests.TestData;

public class Obj : IEquatable<Obj>
{
    [CsvIndex(0)]
    public int Id { get; set; }

    [CsvIndex(1)]
    public string? Name { get; set; }

    [CsvIndex(2)]
    public bool IsEnabled { get; set; }

    [CsvIndex(3)]
    public DateTimeOffset LastLogin { get; set; }

    [CsvIndex(4)]
    public Guid Token { get; set; }

    public bool Equals(Obj? other)
    {
        return other is not null
            && Id.Equals(other.Id)
            && StringComparer.Ordinal.Equals(Name, other.Name)
            && IsEnabled.Equals(other.IsEnabled)
            && LastLogin.Equals(other.LastLogin)
            && Token.Equals(other.Token);
    }

    public override string ToString()
    {
        using ValueStringBuilder vsb = new(stackalloc char[256]);
        vsb.Append("Obj { ");
        vsb.Append(nameof(Id));
        vsb.Append(" = ");
        vsb.AppendFormatted(Id);
        vsb.Append(", ");
        vsb.Append(nameof(Name));
        vsb.Append(" = ");
        vsb.Append(Name);
        vsb.Append(", ");
        vsb.Append(nameof(IsEnabled));
        vsb.Append(" = ");
        vsb.Append(IsEnabled ? "true" : "false");
        vsb.Append(", ");
        vsb.Append(nameof(LastLogin));
        vsb.Append(" = ");
        vsb.AppendFormatted(LastLogin, format: "O");
        vsb.Append(", ");
        vsb.Append(nameof(Token));
        vsb.Append(" = ");
        vsb.AppendFormatted(Token);
        vsb.Append(" }");
        return vsb.ToString();
    }
}

public enum Mode
{
    None = 0,
    RFC = 1,
    Escape = 2,
}

public static class TestDataGenerator
{
    private const int RequiredCapacity = 131_072;

    public const string Header = "Id,Name,IsEnabled,LastLogin,Token";
    public const string HeaderQuoted = "'Id','Name','IsEnabled','LastLogin','Token'";
    public static ReadOnlySpan<byte> HeaderU8 => "Id,Name,IsEnabled,LastLogin,Token"u8;
    public static ReadOnlySpan<byte> HeaderQuotedU8 => "'Id','Name','IsEnabled','LastLogin','Token'"u8;

    public static readonly byte[] GuidBytes = [0, 1, 2, 3, 4, 5, 6, 7];

    private static readonly ConcurrentDictionary<Key, Lazy<ReadOnlyMemory<char>>> _chars = [];
    private static readonly ConcurrentDictionary<Key, Lazy<ReadOnlyMemory<byte>>> _bytes = [];

    private readonly record struct Key(string Newline, bool WriteHeader, bool TrailingNewline, Mode Escaping);

    public static ReadOnlyMemory<T> Generate<T>(
        CsvNewline newLineToken,
        bool writeHeader,
        bool writeTrailingNewline,
        Mode escaping
    )
    {
        if (typeof(T) == typeof(char))
            return (ReadOnlyMemory<T>)(object)GenerateText(newLineToken, writeHeader, writeTrailingNewline, escaping);

        if (typeof(T) == typeof(byte))
            return (ReadOnlyMemory<T>)(object)GenerateBytes(newLineToken, writeHeader, writeTrailingNewline, escaping);

        throw new UnreachableException();
    }

    public static ReadOnlyMemory<char> GenerateText(
        CsvNewline newLineToken,
        bool writeHeader,
        bool writeTrailingNewline,
        Mode escaping
    )
    {
        string newLine = newLineToken.AsString();
        var key = new Key(newLine, writeHeader, writeTrailingNewline, escaping);
        var chars = _chars.GetOrAdd(
            key,
            static key => new Lazy<ReadOnlyMemory<char>>(() =>
            {
                (string newLine, bool writeHeader, bool writeTrailingNewline, Mode escaping) = key;

                var writer = new StringBuilder(capacity: RequiredCapacity);

                if (writeHeader)
                {
                    writer.Append(Header);
                    writer.Append(newLine);
                }

                CancellationToken token = TestContext.Current.CancellationToken;

                for (int i = 0; i < 1_000; i++)
                {
                    token.ThrowIfCancellationRequested();

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
            })
        );

        return chars.Value;
    }

    public static ReadOnlyMemory<byte> GenerateBytes(
        CsvNewline newLineToken,
        bool writeHeader,
        bool writeTrailingNewline,
        Mode escaping
    )
    {
        string newLine = newLineToken.AsString();
        var key = new Key(newLine, writeHeader, writeTrailingNewline, escaping);
        var chars = _bytes.GetOrAdd(
            key,
            static key => new Lazy<ReadOnlyMemory<byte>>(() =>
            {
                (string newLine, bool writeHeader, bool writeTrailingNewline, Mode escaping) = key;

                var innerWriter = new ArrayBufferWriter<byte>(initialCapacity: RequiredCapacity);
                var writer = Utf8StringInterpolation.Utf8String.CreateWriter(innerWriter);

                if (writeHeader)
                {
                    writer.Append(Header);
                    writer.Append(newLine);
                }

                CancellationToken token = TestContext.Current.CancellationToken;

                for (int i = 0; i < 1_000; i++)
                {
                    token.ThrowIfCancellationRequested();

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
            })
        );

        return chars.Value;
    }

    public static readonly Lazy<List<Obj>> Objects = new(
        () =>
        {
            var list = new List<Obj>(1000);

            for (int i = 0; i < 1000; i++)
            {
                list.Add(
                    new Obj
                    {
                        Id = i,
                        IsEnabled = i % 2 == 0,
                        LastLogin = DateTimeOffset.UnixEpoch,
                        Token = new Guid(i, 0, 0, GuidBytes),
                        Name = $" Name'{i}",
                    }
                );
            }

            return list;
        },
        LazyThreadSafetyMode.ExecutionAndPublication
    );
}
