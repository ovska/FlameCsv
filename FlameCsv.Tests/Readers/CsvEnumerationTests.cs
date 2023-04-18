using System.Buffers;
using FlameCsv.Exceptions;
using FlameCsv.Utilities;

namespace FlameCsv.Tests.Readers;

public static class CsvEnumerationTests
{
    [Fact]
    public static void Should_Verify_Enumerator_Version()
    {
        CsvFieldEnumerator<char> enumerator = default;

        ReadOnlySequence<char> multilineData = new("1\r\n2\r\n".AsMemory());
        bool first = true;

        foreach (var record in new CsvEnumerable<char>(multilineData, CsvTextReaderOptions.Default))
        {
            if (first)
            {
                first = false;
                enumerator = record.GetEnumerator();
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
                enumerator = record.GetEnumerator();
            }
        }

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public static void Should_Verify_Parameters()
    {
        Assert.Throws<ArgumentNullException>(() => new CsvEnumerable<char>(default, null!));
        Assert.Throws<ArgumentNullException>(() => new CsvRecord<char>(default, null!));
        Assert.Throws<CsvConfigurationException>(() => new CsvRecord<char>(default, new CsvTextReaderOptions { Quote = ',' }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CsvRecord<char>(default, CsvTextReaderOptions.Default, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CsvRecord<char>(default, CsvTextReaderOptions.Default, 0, -1));
    }

    [Fact]
    public static void Should_Enumerate_Csv()
    {
        using var enumerator = new CsvFieldEnumerator<char>("1,\"Test\",true".AsMemory(), CsvDialect<char>.Default);

        Assert.True(enumerator.MoveNext());
        Assert.Equal("1", enumerator.Current.ToString());

        Assert.True(enumerator.MoveNext());
        Assert.Equal("Test", enumerator.Current.ToString());

        Assert.True(enumerator.MoveNext());
        Assert.Equal("true", enumerator.Current.ToString());

        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public static void Should_Return_Buffer_On_Dispose()
    {
        // dispose throws if no return
        using var pool = new ReturnTrackingArrayPool<char>();

        using (var enumerator = new CsvFieldEnumerator<char>("\"xyz\"".AsMemory(), CsvDialect<char>.Default, pool))
        {
            Assert.True(enumerator.MoveNext());
            Assert.False(enumerator.MoveNext());
            enumerator.Reset();
            Assert.True(enumerator.MoveNext());
            Assert.False(enumerator.MoveNext());
        }
    }

    [Fact]
    public static void Should_Work_On_Empty_Data()
    {
        using var enumerator = new CsvFieldEnumerator<char>(default, CsvDialect<char>.Default);
        Assert.False(enumerator.MoveNext());
        enumerator.Reset();
        Assert.False(enumerator.MoveNext());

        var record = new CsvRecord<char>(default, CsvTextReaderOptions.Default);
        Assert.Empty(record);
        Assert.Equal(0, record.GetFieldCount());
        Assert.ThrowsAny<ArgumentException>(() => record.GetField(0));
    }

    [Fact]
    public static void Should_Enumerate_Record()
    {
        CsvRecord<char> record = GetRecord();

        var expected = new[] { "1", "Test", "true" };
        var actual = new List<string>();

        foreach (var column in record)
            actual.Add(column.ToString());

        Assert.Equal(expected, actual);
    }

    [Fact]
    public static void Should_Parse_Fields()
    {
        CsvRecord<char> record = GetRecord();

        Assert.Equal(1, record.GetField<int>(0));
        Assert.Equal("Test", record.GetField<string?>(1));
        Assert.True(record.GetField<bool>(2));
    }

    [Fact]
    public static void Should_Return_Fields()
    {
        CsvRecord<char> record = GetRecord();

        Assert.Equal("1", record.GetField(0).ToString());
        Assert.Equal("Test", record.GetField(1).ToString());
        Assert.Equal("true", record.GetField(2).ToString());
    }

    [Fact]
    public static void Should_Return_Field_Count()
    {
        CsvRecord<char> record = GetRecord();

        Assert.Equal(3, record.GetFieldCount());
        Assert.Equal(3, record.GetFieldCount());
    }

    private static CsvRecord<char> GetRecord() => new("1,\"Test\",true".AsMemory(), CsvTextReaderOptions.Default);
}
