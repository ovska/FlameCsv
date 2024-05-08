using System.Globalization;
using System.Numerics;
using System.Text;

namespace FlameCsv.Tests.Converters;

public class DefaultTextConverterTests : DefaultConverterTests<char>
{
    protected override CsvOptions<char> Options => CsvTextOptions.Default;
    protected override ReadOnlySpan<char> AsSpan(string? value) => value.AsSpan();

    [Fact(Skip = "Revisit date parsing logic")]
    public void Dates()
    {
        var o = new CsvTextOptions { DateTimeFormat = "O" };

        const string str = "2015-01-02T03:04:05.0000000+00:00";
        var date = new DateTimeOffset(2015, 1, 2, 3, 4, 5, TimeSpan.Zero);

        Execute(str, date.DateTime, o);
        Execute(str, date, o);
    }
}

public class DefaultUtf8ConverterTests : DefaultConverterTests<byte>
{
    protected override CsvOptions<byte> Options => CsvUtf8Options.Default;
    protected override ReadOnlySpan<byte> AsSpan(string? value) => Encoding.UTF8.GetBytes(value ?? "");

    [Fact(Skip = "Revisit date parsing logic")]
    public void Dates()
    {
        var o = new CsvUtf8Options { DateTimeFormat = 'O' };

        const string str = "2015-01-02T03:04:05.0000000+00:00";
        var date = new DateTimeOffset(2015, 1, 2, 3, 4, 5, TimeSpan.Zero);

        Execute(str, date.Date, o);
        Execute(str, date, o);
    }
}

public abstract class DefaultConverterTests<T> where T : unmanaged, IEquatable<T>
{
    private readonly T[] _buffer = new T[128];

    protected abstract CsvOptions<T> Options { get; }
    protected abstract ReadOnlySpan<T> AsSpan(string? value);

    [Fact]
    public void Structs()
    {
        Execute("Monday", DayOfWeek.Monday);
        Execute("", new int?());
        Execute("1", 1);
        Execute("", new DayOfWeek?());
        Execute("Monday", DayOfWeek.Monday);

        var guid = new Guid(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11);
        Execute(guid.ToString("D"), guid);

        Execute("true", true);
        Execute("false", false);
    }

    [Fact]
    public void Floats()
    {
        RunFloat<float>();
        RunFloat<double>();
        RunFloat<decimal>();

        if (typeof(T) != typeof(byte))
            RunFloat<Half>();
    }

    [Fact]
    public void Integers()
    {
        RunInt<short>();
        RunInt<int>();
        RunInt<long>();
        RunInt<nint>();
        RunInt<ushort>();
        RunInt<uint>();
        RunInt<ulong>();
        RunInt<nuint>();
    }

    private void RunFloat<TNumber>() where TNumber : notnull, IFloatingPointConstants<TNumber>, IFloatingPoint<TNumber>, IMinMaxValue<TNumber>
    {
        Execute("0", TNumber.Zero);
        Execute("1", TNumber.One);
        Execute("-1", TNumber.NegativeOne);
        Execute("0.5", TNumber.One / (TNumber.One + TNumber.One));
        Execute(TNumber.MinValue.ToString(null, CultureInfo.InvariantCulture), TNumber.MinValue);
        Execute(TNumber.MaxValue.ToString(null, CultureInfo.InvariantCulture), TNumber.MaxValue);
    }

    private void RunInt<TNumber>() where TNumber : notnull, INumber<TNumber>, IMinMaxValue<TNumber>
    {
        Execute("0", TNumber.Zero);
        Execute("1", TNumber.One);
        Execute(TNumber.MinValue.ToString(null, CultureInfo.InvariantCulture), TNumber.MinValue);
        Execute(TNumber.MaxValue.ToString(null, CultureInfo.InvariantCulture), TNumber.MaxValue);
    }

    protected void Execute<TValue>(string? str, TValue obj, CsvOptions<T>? options = null)
    {
        var span = AsSpan(str);
        var converter = (options ?? Options).GetConverter<TValue>();

        Assert.True(converter.TryParse(span, out var value));
        Assert.Equal(obj, value);
        Assert.True(converter.TryFormat(_buffer, obj, out int charsWritten));
        Assert.Equal(span, _buffer.AsSpan(0, charsWritten));
    }
}
