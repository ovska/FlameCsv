using System.Buffers;
using System.Diagnostics;
using FlameCsv.Utilities;

namespace FlameCsv.Tests.Readers;

public static class CsvEnumerationTests
{
    [Fact]
    public static void Should_Verify_Parameters()
    {
        Assert.Throws<ArgumentNullException>(() => new CsvRecordEnumerable<char>(default(ReadOnlySequence<char>), null!));
    }

    [Fact]
    public static void Should_Enumerate_Csv()
    {
        using var enumerator = new CsvFieldEnumerator<char>("1,\"Test\",true".AsMemory(), CsvTextReaderOptions.Default);

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

        using (var enumerator = new CsvFieldEnumerator<char>("\"xyz\"".AsMemory(), CsvTextReaderOptions.Default))
        {
            Assert.True(enumerator.MoveNext());
            Assert.False(enumerator.MoveNext());
        }
    }

    [Fact]
    public static void Should_Work_On_Empty_Data()
    {
        using var enumerator = new CsvFieldEnumerator<char>(ReadOnlyMemory<char>.Empty, CsvTextReaderOptions.Default);
        Assert.False(enumerator.MoveNext());

        var record = new CsvValueRecord<char>("".AsMemory(), CsvTextReaderOptions.Default);
        Assert.Empty(record);
        Assert.Equal(0, record.GetFieldCount());
        Assert.ThrowsAny<ArgumentException>(() => record.GetField(0));
    }

    [Fact]
    public static void Should_Return_Field_Count()
    {
        CsvValueRecord<char> record = GetRecord();

        Assert.Equal(3, record.GetFieldCount());
        Assert.Equal(3, record.GetFieldCount());
    }

    [Fact]
    public static void Should_Enumerate_Record()
    {
        CsvValueRecord<char> record = GetRecord();

        var expected = new[] { "1", "Test", "true" };
        var actual = new List<string>();

        foreach (var column in record)
            actual.Add(column.ToString());

        Assert.Equal(expected, actual);
    }

    [Fact]
    public static void Should_Parse_Fields()
    {
        CsvValueRecord<char> record = GetRecord();

        Assert.Equal(1, record.GetField<int>(0));
        Assert.Equal("Test", record.GetField<string?>(1));
        Assert.True(record.GetField<bool>(2));

        Assert.True(record.TryGetValue(0, out int _1));
        Assert.Equal(1, _1);

        Assert.True(record.TryGetValue(1, out string? _2));
        Assert.Equal("Test", _2);

        Assert.True(record.TryGetValue(2, out bool _3));
        Assert.True(_3);
    }

    [Fact]
    public static void Should_Return_Fields()
    {
        CsvValueRecord<char> record = GetRecord();

        Assert.Equal("1", record.GetField(0).ToString());
        Assert.Equal("Test", record.GetField(1).ToString());
        Assert.Equal("true", record.GetField(2).ToString());
    }

    [Fact]
    public static void Should_Return_Fields_By_Name()
    {
        using var enumerator = new CsvRecordEnumerable<char>("A,B,C\r\n1,2,3".AsMemory(), new CsvTextReaderOptions { HasHeader = true })
            .GetEnumerator();

        CsvValueRecord<char> record = default;
        int count = 0;

        while (enumerator.MoveNext())
        {
            record = enumerator.Current;
            Assert.Equal(1, ++count);
        }

        Assert.Equal("1", record.GetField("A").ToString());
        Assert.Equal("2", record.GetField("B").ToString());
        Assert.Equal("3", record.GetField("C").ToString());
    }

    [Fact]
    public static void Should_Return_Parse_Failure_Reason()
    {
        using var enumerator = new CsvRecordEnumerable<char>(
            "A,B,C,D\r\n1,2,3".AsMemory(), new CsvTextReaderOptions { HasHeader = true })
            .GetEnumerator();

        CsvValueRecord<char> record = default;
        int count = 0;

        while (enumerator.MoveNext())
        {
            record = enumerator.Current;
            Assert.Equal(1, ++count);
        }

        Assert.False(record.TryGetValue<Stopwatch>(4, out _, out var reason));
        Assert.Equal(CsvGetValueReason.FieldNotFound, reason);

        Assert.False(record.TryGetValue<Stopwatch>(0, out _, out reason));
        Assert.Equal(CsvGetValueReason.NoParserFound, reason);

        Assert.False(record.TryGetValue<bool>(0, out _, out reason));
        Assert.Equal(CsvGetValueReason.UnparsableValue, reason);

        Assert.False(record.TryGetValue<Stopwatch>("E", out _, out reason));
        Assert.Equal(CsvGetValueReason.HeaderNotFound, reason);

        Assert.False(record.TryGetValue<Stopwatch>("D", out _, out reason));
        Assert.Equal(CsvGetValueReason.FieldNotFound, reason);

        Assert.False(record.TryGetValue<bool>("D", out _, out reason));
        Assert.Equal(CsvGetValueReason.FieldNotFound, reason);

        Assert.False(record.TryGetValue<bool>("C", out _, out reason));
        Assert.Equal(CsvGetValueReason.UnparsableValue, reason);
    }

    private static CsvValueRecord<char> GetRecord()
    {
        return new CsvValueRecord<char>("1,\"Test\",true".AsMemory(), CsvTextReaderOptions.Default);
    }
}
