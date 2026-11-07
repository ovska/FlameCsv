using System.Globalization;
using System.Numerics;
using System.Text;
using FlameCsv.Attributes;

namespace FlameCsv.Tests.Binding;

/// <summary>
/// Source generated type maps with enum, nullable, and non-native span parsable/formattable members.
/// </summary>
public partial class BuiltinTypeMapTests
{
    public enum Color
    {
        Red,
        Green,
        Blue,
    }

    [Flags]
    public enum Perms
    {
        None = 0,
        Read = 1,
        Write = 2,
    }

    /// <summary>UTF-16 parsable and formattable only, transcoded when reading/writing UTF-8.</summary>
    public readonly record struct Code(int Value) : ISpanParsable<Code>, ISpanFormattable
    {
        public static Code Parse(string s, IFormatProvider? provider) => new(int.Parse(s, provider));

        public static bool TryParse(string? s, IFormatProvider? provider, out Code result) =>
            TryParse(s.AsSpan(), provider, out result);

        public static Code Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => new(int.Parse(s, provider));

        public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Code result)
        {
            bool success = int.TryParse(s, provider, out int value);
            result = new(value);
            return success;
        }

        public string ToString(string? format, IFormatProvider? formatProvider) =>
            Value.ToString(format, formatProvider);

        public bool TryFormat(
            Span<char> destination,
            out int charsWritten,
            ReadOnlySpan<char> format,
            IFormatProvider? provider
        ) => Value.TryFormat(destination, out charsWritten, format, provider);
    }

    public sealed class Rec
    {
        public int Id { get; set; }
        public Color Color { get; set; }
        public Color? MaybeColor { get; set; }
        public Perms Perms { get; set; }
        public DateOnly Date { get; set; }
        public DateOnly? MaybeDate { get; set; }
        public Int128 Big { get; set; }
        public BigInteger BigInt { get; set; }
        public Code Code { get; set; }
        public Code? MaybeCode { get; set; }

        public override string ToString() =>
            string.Join(
                ";",
                Id,
                Color,
                MaybeColor?.ToString() ?? "null",
                Perms,
                Date.ToString(CultureInfo.InvariantCulture),
                MaybeDate?.ToString(CultureInfo.InvariantCulture) ?? "null",
                Big,
                BigInt,
                Code.Value,
                MaybeCode?.Value.ToString() ?? "null"
            );
    }

    [CsvTypeMap<char, Rec>]
    private partial class RecCharMap;

    [CsvTypeMap<byte, Rec>]
    private partial class RecByteMap;

    private static readonly Rec[] _items =
    [
        new()
        {
            Id = 1,
            Color = Color.Blue,
            MaybeColor = Color.Green,
            Perms = Perms.Read | Perms.Write,
            Date = new(2026, 11, 7),
            MaybeDate = new(2000, 1, 2),
            Big = Int128.MaxValue,
            BigInt = BigInteger.Pow(10, 30),
            Code = new(42),
            MaybeCode = new(-7),
        },
        new()
        {
            Id = 2,
            Color = Color.Red,
            Perms = Perms.None,
            Date = DateOnly.MinValue,
            Big = Int128.MinValue,
            BigInt = -1,
            Code = new(0),
        },
    ];

    private static string Expected => string.Join(Environment.NewLine, _items.Select(x => x.ToString()));

    [Fact]
    public void Should_Write_Same_As_Reflection()
    {
        var sb = new StringBuilder();
        Csv.To(sb).Write(_items);
        string reflection = sb.ToString();

        Assert.Contains("\"Read, Write\"", reflection);

        sb.Clear();
        Csv.To(sb).Write(new RecCharMap(), _items);
        Assert.Equal(reflection, sb.ToString());

        var ms = new MemoryStream();
        Csv.To(ms).Write(new RecByteMap(), _items);
        Assert.Equal(reflection, Encoding.UTF8.GetString(ms.ToArray()));
    }

    [Fact]
    public void Should_Roundtrip_Char()
    {
        var sb = new StringBuilder();
        Csv.To(sb).Write(new RecCharMap(), _items);

        var result = Csv.From(sb.ToString()).Read(new RecCharMap()).ToList();
        Assert.Equal(Expected, string.Join(Environment.NewLine, result.Select(x => x.ToString())));
    }

    [Fact]
    public void Should_Roundtrip_Byte()
    {
        var ms = new MemoryStream();
        Csv.To(ms).Write(new RecByteMap(), _items);

        var result = Csv.From(new ReadOnlyMemory<byte>(ms.ToArray())).Read(new RecByteMap()).ToList();
        Assert.Equal(Expected, string.Join(Environment.NewLine, result.Select(x => x.ToString())));
    }
}
