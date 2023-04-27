using System.Buffers;
using FlameCsv.Binding.Attributes;
using FlameCsv.Utilities;

namespace FlameCsv.Tests.Readers;

public sealed class CsvEnumerationTests : IDisposable
{
    private class Shim
    {
        [CsvIndex(0)] public int Id { get; set; }
        [CsvIndex(1)] public string? Name { get; set; }
    }

    [Fact]
    public void Should_Parse_Record()
    {
        const string data = "1,Bob\r\n2,Alice";

        int index = 0;

        foreach (var record in new CsvRecordEnumerable<char>(data.AsMemory(), CsvTextReaderOptions.Default))
        {
            var shim = record.ParseRecord<Shim>();
            Assert.Equal(index + 1, shim.Id);
            Assert.Equal(index == 0 ? "Bob" : "Alice", shim.Name);
            index++;
        }
    }

    [Fact]
    public void Should_Verify_Parameters()
    {
        Assert.Throws<ArgumentNullException>(() => new CsvRecordEnumerable<char>(default(ReadOnlySequence<char>), null!));
    }

    [Fact]
    public void Should_Enumerate_Csv()
    {
        using var enumerator = new CsvFieldEnumerator<char>("1,\"Test\",true".AsMemory(), CsvTextReaderOptions.Default.ToContext());

        Assert.True(enumerator.MoveNext());
        Assert.Equal("1", enumerator.Current.ToString());

        Assert.True(enumerator.MoveNext());
        Assert.Equal("Test", enumerator.Current.ToString());

        Assert.True(enumerator.MoveNext());
        Assert.Equal("true", enumerator.Current.ToString());

        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void Should_Return_Buffer_On_Dispose()
    {
        // dispose throws if no return
        using var pool = new ReturnTrackingArrayPool<char>();

        using (var enumerator = new CsvFieldEnumerator<char>("\"xyz\"".AsMemory(), CsvTextReaderOptions.Default.ToContext()))
        {
            Assert.True(enumerator.MoveNext());
            Assert.False(enumerator.MoveNext());
        }
    }

    //[Fact]
    //public  void Should_Work_On_Empty_Data()
    //{
    //    using var enumerator = new CsvFieldEnumerator<char>(ReadOnlyMemory<char>.Empty, CsvTextReaderOptions.Default);
    //    Assert.False(enumerator.MoveNext());

    //    var record = new CsvValueRecord<char>("".AsMemory(), CsvTextReaderOptions.Default);
    //    Assert.Equal(0, record.GetFieldCount());
    //    Assert.ThrowsAny<ArgumentException>(() => record.GetField(0));
    //}

    [Fact]
    public void Should_Return_Field_Count()
    {
        CsvValueRecord<char> record = GetRecord();

        Assert.Equal(3, record.GetFieldCount());
        Assert.Equal(3, record.GetFieldCount());
    }

    [Fact]
    public void Should_Enumerate_Record()
    {
        CsvValueRecord<char> record = GetRecord();

        var expected = new[] { "1", "Test", "true" };
        var actual = new List<string>();

        foreach (var field in record)
            actual.Add(field.ToString());

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Should_Parse_Fields()
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
    public void Should_Return_Fields()
    {
        CsvValueRecord<char> record = GetRecord();

        Assert.Equal("1", record.GetField(0).ToString());
        Assert.Equal("Test", record.GetField(1).ToString());
        Assert.Equal("true", record.GetField(2).ToString());
    }

    [Fact]
    public void Should_Return_Fields_By_Name()
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

    private CsvValueRecord<char> GetRecord()
    {
        var enumerator = new CsvRecordEnumerator<char>("1,\"Test\",true".AsMemory(), CsvTextReaderOptions.Default);
        _enumerator = enumerator;
        enumerator.MoveNext();
        return enumerator.Current;
    }

    private IDisposable? _enumerator;

    public void Dispose() => _enumerator?.Dispose();
}
