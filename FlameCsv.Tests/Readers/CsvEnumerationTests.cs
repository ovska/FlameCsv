using System.Buffers;
using FlameCsv.Binding.Attributes;
using FlameCsv.Enumeration;
using FlameCsv.Tests.Utilities;

namespace FlameCsv.Tests.Readers;

public sealed class CsvEnumerationTests : IDisposable
{
    private class Shim
    {
        [CsvIndex(0)] public int Id { get; set; }
        [CsvIndex(1)] public string? Name { get; set; }
    }

    [Fact]
    public void Should_Reset_Header()
    {
        const string data =
            "id,name\r\n" +
            "1,Bob\r\n" +
            "\r\n" +
            "name,id\r\n" +
            "Alice,2\r\n";

        using var enumerator = new CsvRecordEnumerator<char>(data.ToArray(), CsvOptions<char>.Default);

        Assert.True(enumerator.MoveNext());
        var record1 = enumerator.Current.ParseRecord<Shim>();
        Assert.Equal(1, record1.Id);
        Assert.Equal("Bob", record1.Name);

        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.Current.IsEmpty);
        enumerator.Current.ResetHeader();

        Assert.True(enumerator.MoveNext());
        var record2 = enumerator.Current.ParseRecord<Shim>();
        Assert.Equal(2, record2.Id);
        Assert.Equal("Alice", record2.Name);
    }

    [Fact]
    public void Should_Enumerate_Lines()
    {
        const string data = "1,\"Test\",true\r\n2,\"Asd\",false\r\n";

        using var enumerator = new CsvRecordEnumerator<char>(data.ToArray(), new CsvOptions<char> { HasHeader = false });

        Assert.True(enumerator.MoveNext());
        Assert.Equal("1,\"Test\",true", enumerator.Current.RawRecord.ToString());
        Assert.Equal(1, enumerator.Current.GetField<int>(0));
        Assert.Equal("Test", enumerator.Current.GetField<string>(1));
        Assert.True(enumerator.Current.GetField<bool>(2));

        Assert.True(enumerator.MoveNext());
        Assert.Equal("2,\"Asd\",false", enumerator.Current.RawRecord.ToString());
        Assert.Equal(2, enumerator.Current.GetField<int>(0));
        Assert.Equal("Asd", enumerator.Current.GetField<string>(1));
        Assert.False(enumerator.Current.GetField<bool>(2));
    }

    [Fact]
    public void Should_Parse_Record()
    {
        const string data = "1,Bob\r\n2,Alice";

        int index = 0;

        foreach (var record in new CsvRecordEnumerable<char>(data.AsMemory(), new CsvOptions<char> { HasHeader = false }))
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

    //[Fact]
    //public void Should_Enumerate_Fields()
    //{
    //    using var enumerator = new CsvFieldEnumerator<char>(
    //        "1,\"Test\",true".AsMemory(),
    //        CsvOptions<char>.Default);

    //    Assert.True(enumerator.MoveNext());
    //    Assert.Equal("1", enumerator.Current.ToString());

    //    Assert.True(enumerator.MoveNext());
    //    Assert.Equal("Test", enumerator.Current.ToString());

    //    Assert.True(enumerator.MoveNext());
    //    Assert.Equal("true", enumerator.Current.ToString());

    //    Assert.False(enumerator.MoveNext());
    //}

    //[Fact]
    //public void Should_Return_Buffer_On_Dispose()
    //{
    //    // dispose throws if no return
    //    using var pool = new ReturnTrackingArrayPool<char>();

    //    using var enumerator = new CsvFieldEnumerator<char>(
    //        "\"xyz\"".AsMemory(),
    //        CsvOptions<char>.Default);

    //    Assert.True(enumerator.MoveNext());
    //    Assert.False(enumerator.MoveNext());
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
        using var enumerator = new CsvRecordEnumerable<char>("A,B,C\r\n1,2,3".AsMemory(), new CsvOptions<char> { HasHeader = true })
            .GetEnumerator();

        Assert.True(enumerator.MoveNext());
        CsvValueRecord<char> record = enumerator.Current;

        Assert.Equal("1", record.GetField("A").ToString());
        Assert.Equal("2", record.GetField("B").ToString());
        Assert.Equal("3", record.GetField("C").ToString());

        Assert.False(enumerator.MoveNext());
    }

    private CsvValueRecord<char> GetRecord()
    {
        _enumerator = new CsvRecordEnumerator<char>(
            "1,\"Test\",true".AsMemory(),
            new CsvOptions<char> { HasHeader = false });
        _enumerator.MoveNext();
        return _enumerator.Current;
    }

    private CsvRecordEnumerator<char>? _enumerator;

    public void Dispose() => _enumerator?.Dispose();
}
