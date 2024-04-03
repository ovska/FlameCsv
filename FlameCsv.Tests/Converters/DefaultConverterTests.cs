using System.Numerics;
using System.Text;

namespace FlameCsv.Tests.Converters;

public class DefaultTextConverterTests : DefaultConverterTests<char>
{
    protected override CsvOptions<char> Options => CsvTextOptions.Default;
    protected override ReadOnlySpan<char> AsSpan(string? value) => value.AsSpan();
}

public class DefaultUtf8ConverterTests : DefaultConverterTests<byte>
{
    protected override CsvOptions<byte> Options => CsvUtf8Options.Default;
    protected override ReadOnlySpan<byte> AsSpan(string? value) => Encoding.UTF8.GetBytes(value ?? "");
}

public abstract class DefaultConverterTests<T> where T : unmanaged, IEquatable<T>
{
    protected abstract CsvOptions<T> Options { get; }
    protected abstract ReadOnlySpan<T> AsSpan(string? value);

    [Fact]
    public void Integers()
    {
        Run<short>();
        Run<int>();
        Run<long>();
        Run<nint>();
        Run<ushort>();
        Run<uint>();
        Run<ulong>();
        Run<nuint>();

        void Run<TNumber>() where TNumber : notnull, IBinaryInteger<TNumber>, IMinMaxValue<TNumber>
        {
            var c = Options.GetConverter<TNumber>();

            (string?, TNumber)[] asserts =
            [
                ("0", TNumber.Zero),
                ("1", TNumber.One),
                (TNumber.MinValue.ToString(), TNumber.MinValue),
                (TNumber.MaxValue.ToString(), TNumber.MaxValue),
            ];

            Span<T> buffer = stackalloc T[64];

            foreach (var (str, obj) in asserts)
            {
                var span = AsSpan(str);

                Assert.True(c.TryParse(span, out var value));
                Assert.Equal(obj, value);

                Assert.True(c.TryFormat(buffer, obj, out int charsWritten));
                Assert.Equal(span, buffer[..charsWritten]);
            }
        }
    }
}
