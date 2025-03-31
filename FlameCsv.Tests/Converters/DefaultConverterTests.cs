using System.Globalization;
using System.Text;

namespace FlameCsv.Tests.Converters;

public class DefaultTextConverterTests : DefaultConverterTests<char>
{
    protected override ReadOnlySpan<char> AsSpan(string? value) => value.AsSpan();
}

public class DefaultUtf8ConverterTests : DefaultConverterTests<byte>
{
    protected override ReadOnlySpan<byte> AsSpan(string? value) => Encoding.UTF8.GetBytes(value ?? "");
}

public abstract class DefaultConverterTests<T> where T : unmanaged, IBinaryInteger<T>
{
    private readonly T[] _buffer = new T[128];

    protected abstract ReadOnlySpan<T> AsSpan(string? value);

    [Fact]
    public void NotEnoughSpace()
    {
        var o = new CsvOptions<T> { Null = "null" };

        ExecuteLocal(true);
        ExecuteLocal(default(string?));
        ExecuteLocal(DayOfWeek.Monday);
        ExecuteLocal(new int?());
        ExecuteLocal(new DayOfWeek?());
        ExecuteLocal((DayOfWeek?)DayOfWeek.Monday);
        ExecuteLocal(1);
        ExecuteLocal(1u);
        ExecuteLocal(1d);
        ExecuteLocal(1f);
        ExecuteLocal(DateTime.UnixEpoch);
        ExecuteLocal(Guid.Empty);

        void ExecuteLocal<TValue>(TValue obj)
        {
            var converter = o.GetConverter<TValue>();

            if (obj is not null || converter.CanFormatNull)
                Assert.False(converter.TryFormat([], obj, out _));
        }
    }

    [Fact]
    public void Timespans()
    {
        var o = new CsvOptions<T> { Formats = { { typeof(TimeSpan), "c" } } };
        Execute("00:00:00", TimeSpan.Zero, o);
        Execute("01:23:45", new TimeSpan(1, 23, 45), o);
    }

    [Fact]
    public void Dates()
    {
        var o = new CsvOptions<T> { Formats = { { typeof(DateTimeOffset), "O" } } };
        Execute("2015-01-02T03:04:05.0000000+00:00", new DateTimeOffset(2015, 1, 2, 3, 4, 5, TimeSpan.Zero), o);
    }

    [Fact]
    public void Structs()
    {
        Execute("Monday", DayOfWeek.Monday);
        Execute("", new int?());
        Execute("1", 1);
        Execute("", new DayOfWeek?());
        Execute("Monday", (DayOfWeek?)DayOfWeek.Monday);

        var guid = new Guid(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11);
        Execute(guid.ToString("D"), guid);

        Execute("true", true);
        Execute("false", false);

        Execute("x", 'x');
    }

    [Fact]
    public void Floats()
    {
        RunFloat<float>();
        RunFloat<double>();
        RunFloat<decimal>();
        RunFloat<Half>();
    }

    [Fact]
    public void Integers()
    {
        RunInt<byte>();
        RunInt<sbyte>();
        RunInt<short>();
        RunInt<int>();
        RunInt<long>();
        RunInt<nint>();
        RunInt<ushort>();
        RunInt<uint>();
        RunInt<ulong>();
        RunInt<nuint>();
    }

    private void RunFloat<TNumber>() where TNumber : IFloatingPointConstants<TNumber>, IFloatingPoint<TNumber>, IMinMaxValue<TNumber>
    {
        Execute("0", TNumber.Zero);
        Execute("1", TNumber.One);
        Execute("-1", TNumber.NegativeOne);
        Execute("0.5", TNumber.One / (TNumber.One + TNumber.One));
        Execute(TNumber.MinValue.ToString(null, CultureInfo.InvariantCulture), TNumber.MinValue);
        Execute(TNumber.MaxValue.ToString(null, CultureInfo.InvariantCulture), TNumber.MaxValue);
    }

    private void RunInt<TNumber>() where TNumber : INumber<TNumber>, IMinMaxValue<TNumber>
    {
        Execute("0", TNumber.Zero);
        Execute("1", TNumber.One);
        Execute(TNumber.MinValue.ToString(null, CultureInfo.InvariantCulture), TNumber.MinValue);
        Execute(TNumber.MaxValue.ToString(null, CultureInfo.InvariantCulture), TNumber.MaxValue);
    }

    protected void Execute<TValue>(string? str, TValue obj, CsvOptions<T>? options = null)
    {
        var span = AsSpan(str);
        var converter = (options ?? CsvOptions<T>.Default).GetConverter<TValue>();

        Assert.True(converter.TryParse(span, out var value));
        Assert.Equal(obj, value);
        Assert.True(converter.TryFormat(_buffer, obj, out int charsWritten));
        Assert.Equal(span, _buffer.AsSpan(0, charsWritten));
    }
}
