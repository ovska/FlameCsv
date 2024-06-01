using System.Globalization;
using System.Numerics;
using System.Text;
using FlameCsv.Converters;

namespace FlameCsv.Tests.Converters;

public class DefaultTextConverterTests : DefaultConverterTests<char>
{
    protected override CsvOptions<char> Options => CsvTextOptions.Default;
    protected override ReadOnlySpan<char> AsSpan(string? value) => value.AsSpan();

    [Fact]
    public void Should_Cache_Defaults()
    {
        var o = new CsvTextOptions();

        Check(DefaultConverters.CreateBoolean);
        Check(DefaultConverters.CreateString);
        Check(DefaultConverters.CreateDateTime);
        Check(DefaultConverters.CreateDateTimeOffset);
        Check(DefaultConverters.CreateGuid);
        Check(DefaultConverters.CreateTimeSpan);
        Check(DefaultConverters.Create<DayOfWeek>);
        Check(DefaultConverters.CreateBoolean);
        Check(DefaultConverters.CreateBoolean);
        Check(DefaultConverters.CreateBoolean);
        Check(DefaultConverters.CreateByte);
        Check(DefaultConverters.CreateSByte);
        Check(DefaultConverters.CreateInt16);
        Check(DefaultConverters.CreateUInt16);
        Check(DefaultConverters.CreateInt32);
        Check(DefaultConverters.CreateUInt32);
        Check(DefaultConverters.CreateInt64);
        Check(DefaultConverters.CreateUInt64);
        Check(DefaultConverters.CreateIntPtr);
        Check(DefaultConverters.CreateUIntPtr);
        Check(DefaultConverters.CreateFloat);
        Check(DefaultConverters.CreateDouble);
        Check(DefaultConverters.CreateDecimal);
        Check(DefaultConverters.CreateHalf);
        Check(o => DefaultConverters.GetOrCreate(o, o => new NullableConverter<char, int>(DefaultConverters.CreateInt32(o))));

        void Check<T>(Func<CsvTextOptions, CsvConverter<char, T>> factory)
        {
            var o = new CsvTextOptions();
            var a = factory(o);
            var b = factory(o);
            Assert.Same(a, b);

            o = new CsvTextOptions();
            a = factory(o);
            b = o.GetConverter<T>();
            Assert.Same(a, b);

            o = new CsvTextOptions();
            a = o.GetConverter<T>();
            b = factory(o);
            Assert.Same(a, b);
        }
    }

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


    [Fact]
    public void Should_Cache_Defaults()
    {
        Check(DefaultConverters.CreateBoolean);
        Check(DefaultConverters.CreateString);
        Check(DefaultConverters.CreateDateTime);
        Check(DefaultConverters.CreateDateTimeOffset);
        Check(DefaultConverters.CreateGuid);
        Check(DefaultConverters.CreateTimeSpan);
        Check(DefaultConverters.Create<DayOfWeek>);
        Check(DefaultConverters.CreateBoolean);
        Check(DefaultConverters.CreateBoolean);
        Check(DefaultConverters.CreateBoolean);
        Check(DefaultConverters.CreateByte);
        Check(DefaultConverters.CreateSByte);
        Check(DefaultConverters.CreateInt16);
        Check(DefaultConverters.CreateUInt16);
        Check(DefaultConverters.CreateInt32);
        Check(DefaultConverters.CreateUInt32);
        Check(DefaultConverters.CreateInt64);
        Check(DefaultConverters.CreateUInt64);
        Check(DefaultConverters.CreateIntPtr);
        Check(DefaultConverters.CreateUIntPtr);
        Check(DefaultConverters.CreateFloat);
        Check(DefaultConverters.CreateDouble);
        Check(DefaultConverters.CreateDecimal);
        Check(DefaultConverters.CreateHalf);
        Check(o => DefaultConverters.GetOrCreate(o, o => new NullableConverter<byte, int>(DefaultConverters.CreateInt32(o))));

        void Check<T>(Func<CsvUtf8Options, CsvConverter<byte, T>> factory)
        {
            var o = new CsvUtf8Options();
            var a = factory(o);
            var b = factory(o);
            Assert.Same(a, b);

            o = new CsvUtf8Options();
            a = factory(o);
            b = o.GetConverter<T>();
            Assert.Same(a, b);

            o = new CsvUtf8Options();
            a = o.GetConverter<T>();
            b = factory(o);
            Assert.Same(a, b);
        }
    }

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
