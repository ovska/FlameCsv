using FlameCsv.Exceptions;

namespace FlameCsv.Tests;

public static class DialectTests
{
    [Fact]
    public static void Should_Return_Default_Options()
    {
        static CsvDialect<T> Default<T>() where T : unmanaged, IEquatable<T> => CsvOptions<T>.Default.Dialect;

        Assert.Equal(',', Default<char>().Delimiter);
        Assert.Equal('"', Default<char>().Quote);
        Assert.Null(Default<char>().Escape);
        Assert.Empty(Default<char>().Newline.ToArray());

        Assert.Equal((byte)',', Default<byte>().Delimiter);
        Assert.Equal((byte)'"', Default<byte>().Quote);
        Assert.Null(Default<byte>().Escape);
        Assert.Empty(Default<byte>().Newline.ToArray());

        Assert.Throws<NotSupportedException>(() => new CsvOptions<sbyte>().Dialect);
    }

    [Fact]
    public static void Should_Validate()
    {
        Assert.Throws<CsvConfigurationException>(() => default(CsvDialect<char>).Validate());

        AssertInvalid(o => o with { Quote = ',' });
        AssertInvalid(o => o with { Newline = ",".AsMemory() });
        AssertInvalid(o => o with { Newline = "\"".AsMemory() });
        AssertInvalid(o => o with { Delimiter = '\n' });
        AssertInvalid(o => o with { Escape = ',' });
        AssertInvalid(o => o with { Whitespace = ",".AsMemory() });

        static void AssertInvalid(Func<CsvDialect<char>, CsvDialect<char>> action)
        {
            Assert.Throws<CsvConfigurationException>(() =>
            {
                action(new CsvOptions<char>().Dialect).Validate();
            });
        }
    }
}
