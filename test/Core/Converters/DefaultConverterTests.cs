using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using FlameCsv.Binding;
using FlameCsv.Converters;
using FlameCsv.Converters.Formattable;

namespace FlameCsv.Tests.Converters;

public class DefaultTextConverterTests : DefaultConverterTests<char>
{
    protected override ReadOnlySpan<char> AsSpan(string? value) => value.AsSpan();

    protected override CsvConverter<char, TValue> GetDefault<TValue>(CsvOptions<char>? options = null)
    {
        var factory = DefaultConverters.GetText(typeof(TValue));
        Assert.True(factory is not null, $"No UTF-8 converter found for type {typeof(TValue)}");
        return (CsvConverter<char, TValue>)factory(options ?? CsvOptions<char>.Default);
    }

    protected override CsvConverterFactory<char> SpanFactory => SpanTextConverterFactory.Instance;
    protected override Type SpanConverterType => typeof(SpanTextConverter<>);
    protected override Type NumberConverterType => typeof(NumberTextConverter<>);
}

public class DefaultUtf8ConverterTests : DefaultConverterTests<byte>
{
    protected override ReadOnlySpan<byte> AsSpan(string? value) => Encoding.UTF8.GetBytes(value ?? "");

    protected override CsvConverter<byte, TValue> GetDefault<TValue>(CsvOptions<byte>? options = null)
    {
        var factory = DefaultConverters.GetUtf8(typeof(TValue));
        Assert.True(factory is not null, $"No UTF-8 converter found for type {typeof(TValue)}");
        return (CsvConverter<byte, TValue>)factory(options ?? CsvOptions<byte>.Default);
    }

    protected override CsvConverterFactory<byte> SpanFactory => SpanUtf8ConverterFactory.Instance;
    protected override Type SpanConverterType => typeof(SpanUtf8Converter<>);
    protected override Type NumberConverterType => typeof(NumberUtf8Converter<>);
}

public abstract class DefaultConverterTests<T>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly T[] _buffer = new T[128];

    protected abstract ReadOnlySpan<T> AsSpan(string? value);
    protected abstract CsvConverterFactory<T> SpanFactory { get; }
    protected abstract Type SpanConverterType { get; }
    protected abstract Type NumberConverterType { get; }
    protected abstract CsvConverter<T, TValue> GetDefault<TValue>(CsvOptions<T>? options = null);

    [Fact]
    public void Base64()
    {
        byte[] input = "Hello, world!"u8.ToArray();
        string b64 = Convert.ToBase64String(input);
        T[] buffer = new T[128];

        var segment = GetDefault<ArraySegment<byte>>();
        Assert.True(segment.TryParse(AsSpan(b64), out var value));
        Assert.Equal(input, value.ToArray());
        Assert.True(segment.TryFormat(buffer, value, out int charsWritten));
        Assert.Equal(b64, CsvOptions<T>.Default.GetAsString(buffer.AsSpan(..charsWritten)));

        var memory = GetDefault<Memory<byte>>();
        Assert.True(memory.TryParse(AsSpan(b64), out var memoryValue));
        Assert.Equal(input, memoryValue.ToArray());
        Assert.True(memory.TryFormat(buffer, memoryValue, out charsWritten));
        Assert.Equal(b64, CsvOptions<T>.Default.GetAsString(buffer.AsSpan(..charsWritten)));

        var readOnlyMemory = GetDefault<ReadOnlyMemory<byte>>();
        Assert.True(readOnlyMemory.TryParse(AsSpan(b64), out var readOnlyMemoryValue));
        Assert.Equal(input, readOnlyMemoryValue.ToArray());
        Assert.True(readOnlyMemory.TryFormat(buffer, readOnlyMemoryValue, out charsWritten));
        Assert.Equal(b64, CsvOptions<T>.Default.GetAsString(buffer.AsSpan(..charsWritten)));

        var array = GetDefault<byte[]>();
        Assert.True(array.TryParse(AsSpan(b64), out var arrayValue));
        Assert.Equal(input, arrayValue);
        Assert.True(array.TryFormat(buffer, arrayValue, out charsWritten));
        Assert.Equal(b64, CsvOptions<T>.Default.GetAsString(buffer.AsSpan(..charsWritten)));
    }

    [Fact]
    public void Ignored()
    {
        Assert.True(CsvIgnored.Converter<T, int>().CanFormatNull);
        Assert.True(CsvIgnored.Converter<T, int>().TryParse([], out _));
        Assert.True(CsvIgnored.Converter<T, int>().TryFormat(_buffer, 0, out int charsWritten));
        Assert.Equal(0, charsWritten);
    }

    [Fact]
    public void SpanFormattableParsable()
    {
        Assert.True(SpanFactory.CanConvert(typeof(int)));
        var c = SpanFactory.Create(typeof(int), CsvOptions<T>.Default);
        Assert.IsType<CsvConverter<T, int>>(c, exactMatch: false);
        Assert.True(c.GetType().GetGenericTypeDefinition() == NumberConverterType);

        Assert.True(SpanFactory.CanConvert(typeof(float)));
        c = SpanFactory.Create(typeof(float), CsvOptions<T>.Default);
        Assert.IsType<CsvConverter<T, float>>(c, exactMatch: false);
        Assert.True(c.GetType().GetGenericTypeDefinition() == NumberConverterType);

        Assert.True(SpanFactory.CanConvert(typeof(char)));
        c = SpanFactory.Create(typeof(char), CsvOptions<T>.Default);
        Assert.IsType<CsvConverter<T, char>>(c, exactMatch: false);
        Assert.True(c.GetType().GetGenericTypeDefinition() == SpanConverterType);

        Assert.False(SpanFactory.CanConvert(typeof(void)));
    }

    [Fact]
    public void NotEnoughSpace()
    {
        var o = new CsvOptions<T> { Null = "null" };

        ExecuteLocal(true);
        ExecuteLocal(false);
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
            {
                Assert.False(converter.TryFormat([], obj, out _));
            }
        }
    }

    [Fact]
    public void Timespans()
    {
        var o = new CsvOptions<T> { Formats = { { typeof(TimeSpan), "c" } } };
        Execute("00:00:00", TimeSpan.Zero, o);
        Execute("01:23:45", new TimeSpan(1, 23, 45), o);
        ExecuteInvalid(TimeSpan.Zero, o);
    }

    [Fact]
    public void Dates()
    {
        var o = new CsvOptions<T> { Formats = { { typeof(DateTimeOffset), "O" } } };
        Execute("2015-01-02T03:04:05.0000000+00:00", new DateTimeOffset(2015, 1, 2, 3, 4, 5, TimeSpan.Zero), o);
        ExecuteInvalid(new DateTimeOffset(2015, 1, 2, 3, 4, 5, TimeSpan.Zero), o);
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
        ExecuteInvalid(guid);

        Execute("true", true);
        Execute("false", false);
        ExecuteInvalid(true);
        ExecuteInvalid(false);

        Execute("x", 'x');
        ExecuteInvalid('x');
    }

    [Fact]
    public void Floats()
    {
        RunFloat<float>();
        RunFloat<double>();
        RunFloat<decimal>();
    }

    [Fact]
    public void Integers()
    {
        RunInt<byte>();
        RunInt<short>();
        RunInt<int>();
        RunInt<long>();
        RunInt<nint>();
        RunInt<ushort>();
        RunInt<uint>();
        RunInt<ulong>();
        RunInt<nuint>();
    }

    [Fact]
    public void NumberConverter_Should_Have_Defaults()
    {
        DoAssertion(GetDefault<int>(), NumberStyles.Integer);
        DoAssertion(GetDefault<float>(), NumberStyles.Float);
        DoAssertion(GetDefault<double>(), NumberStyles.Float);
        DoAssertion(GetDefault<decimal>(), NumberStyles.Float);
        DoAssertion(GetDefault<byte>(), NumberStyles.Integer);
        DoAssertion(GetDefault<short>(), NumberStyles.Integer);
        DoAssertion(GetDefault<ushort>(), NumberStyles.Integer);
        DoAssertion(GetDefault<int>(), NumberStyles.Integer);
        DoAssertion(GetDefault<uint>(), NumberStyles.Integer);
        DoAssertion(GetDefault<long>(), NumberStyles.Integer);
        DoAssertion(GetDefault<ulong>(), NumberStyles.Integer);

        static void DoAssertion(CsvConverter<T> converter, NumberStyles expected)
        {
            Assert.Equal(
                expected,
                converter
                    .GetType()
                    .GetField("_styles", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(converter)
            );
        }
    }

    private void RunFloat<TNumber>()
        where TNumber : IFloatingPointConstants<TNumber>, IFloatingPoint<TNumber>, IMinMaxValue<TNumber>
    {
        Execute("0", TNumber.Zero);
        Execute("1", TNumber.One);
        Execute("-1", TNumber.NegativeOne);
        Execute("0.5", TNumber.One / (TNumber.One + TNumber.One));
        Execute(TNumber.MinValue.ToString(null, CultureInfo.InvariantCulture), TNumber.MinValue);
        Execute(TNumber.MaxValue.ToString(null, CultureInfo.InvariantCulture), TNumber.MaxValue);
        ExecuteInvalid(TNumber.Zero);
    }

    private void RunInt<TNumber>()
        where TNumber : INumber<TNumber>, IMinMaxValue<TNumber>
    {
        Execute("0", TNumber.Zero);
        Execute("1", TNumber.One);
        Execute(TNumber.MinValue.ToString(null, CultureInfo.InvariantCulture), TNumber.MinValue);
        Execute(TNumber.MaxValue.ToString(null, CultureInfo.InvariantCulture), TNumber.MaxValue);
        ExecuteInvalid(TNumber.Zero);
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

    protected void ExecuteInvalid<TValue>(TValue obj, CsvOptions<T>? options = null)
    {
        Span<T> span = [];
        var converter = (options ?? CsvOptions<T>.Default).GetConverter<TValue>();

        Assert.False(converter.TryParse(span, out _));
        Assert.False(converter.TryFormat([], obj, out int charsWritten));
        Assert.Equal(0, charsWritten);
    }
}
