using System.Globalization;
using FlameCsv.Attributes;
using FlameCsv.Binding;
using FlameCsv.Converters.Formattable;
using FlameCsv.Enumeration;

namespace FlameCsv.Tests.Reading;

public sealed class CsvPreserveTests
{
    private class Shim
    {
        [CsvIndex(0)] public int Id { get; set; }
        [CsvIndex(1)] public string? Name { get; set; }
    }

    [Fact]
    public void Should_Enumerate_Lines()
    {
        const string data = "1,\"Test\",true\r\n2,\"Asd\",false\r\n";

        using var enumerator = new CsvRecordEnumerable<char>(
                data.AsMemory(),
                new CsvOptions<char> { HasHeader = false })
            .Preserve()
            .GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Equal("1,\"Test\",true", enumerator.Current.RawRecord.ToString());
        Assert.Equal(1, enumerator.Current.ParseField<int>(0));
        Assert.Equal("Test", enumerator.Current.ParseField<string>(1));
        Assert.True(enumerator.Current.ParseField<bool>(2));

        Assert.True(enumerator.MoveNext());
        Assert.Equal("2,\"Asd\",false", enumerator.Current.RawRecord.ToString());
        Assert.Equal(2, enumerator.Current.ParseField<int>(0));
        Assert.Equal("Asd", enumerator.Current.ParseField<string>(1));
        Assert.False(enumerator.Current.ParseField<bool>(2));
    }

    [Fact]
    public void Should_Parse_Record()
    {
        const string data = "1,Bob\r\n2,Alice";

        int index = 0;

        foreach (var record in new CsvRecordEnumerable<char>(
                         data.AsMemory(),
                         new CsvOptions<char> { HasHeader = false })
                     .Preserve())
        {
            var shim = record.ParseRecord<Shim>();
            Assert.Equal(index + 1, shim.Id);
            Assert.Equal(index == 0 ? "Bob" : "Alice", shim.Name);
            index++;
        }
    }

    [Fact]
    public void Should_Return_Field_Count()
    {
        CsvPreservedRecord<char> record = GetRecord();

        Assert.Equal(3, record.FieldCount);
    }

    [Fact]
    public void Should_Enumerate_Record()
    {
        CsvPreservedRecord<char> record = GetRecord();

        var actual = new List<string>();

        foreach ((_, ReadOnlyMemory<char> field) in record)
            actual.Add(field.ToString());

        Assert.Equal(["1", "Test", "true"], actual);
    }

    [Fact]
    public void Should_Parse_Fields()
    {
        CsvPreservedRecord<char> record = GetRecord();

        Assert.Equal(1, record.ParseField<int>(0));
        Assert.Equal("Test", record.ParseField<string?>(1));
        Assert.True(record.ParseField<bool>(2));

        Assert.True(record.TryParseField(0, out int _1));
        Assert.Equal(1, _1);

        Assert.True(record.TryParseField(1, out string? _2));
        Assert.Equal("Test", _2);

        Assert.True(record.TryParseField(2, out bool _3));
        Assert.True(_3);

        Assert.True(
            record.TryParseField(new NumberTextConverter<int>(record.Options, NumberStyles.Integer), 0, out _1));
        Assert.Equal(1, _1);
    }

    [Fact]
    public void Should_Return_Fields()
    {
        CsvPreservedRecord<char> record = GetRecord();

        Assert.Equal("1", record[0].ToString());
        Assert.Equal("Test", record[1].ToString());
        Assert.Equal("true", record[2].ToString());

        Assert.Equal("1", record.GetField(0).ToString());
        Assert.Equal("Test", record.GetField(1).ToString());
        Assert.Equal("true", record.GetField(2).ToString());
    }

    [Fact]
    public void Should_Return_Fields_By_Name()
    {
        using var enumerator = new CsvRecordEnumerable<char>(
                "A,B,C\r\n1,2,3".AsMemory(),
                new CsvOptions<char> { HasHeader = true })
            .Preserve()
            .GetEnumerator();

        Assert.True(enumerator.MoveNext());
        CsvPreservedRecord<char> record = enumerator.Current;

        Assert.Equal("1", record.GetField("A").ToString());
        Assert.Equal("2", record.GetField("B").ToString());
        Assert.Equal("3", record.GetField("C").ToString());

        Assert.Equal("1", record["A"].ToString());
        Assert.Equal("2", record["B"].ToString());
        Assert.Equal("3", record["C"].ToString());

        Assert.True(record.Contains(0));
        Assert.True(record.Contains(1));
        Assert.True(record.Contains(2));
        Assert.False(record.Contains(3));

        Assert.True(record.Contains("A"));
        Assert.True(record.Contains("B"));
        Assert.True(record.Contains("C"));
        Assert.False(record.Contains("D"));

        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void Should_Throw_If_No_Header()
    {
        Assert.Throws<NotSupportedException>(() => GetRecord().ParseField<int>("A"));
        Assert.Throws<NotSupportedException>(() => GetRecord().ParseField(CsvIgnored.Converter<char, object>(), "A"));
    }

    [Fact]
    public void Should_Implement_Interfaces()
    {
        CsvPreservedRecord<char> record = CsvReader.Enumerate("A,B,C\r\n1,2,3").Preserve().Single();

        Assert.Equal(3, ((IReadOnlyCollection<KeyValuePair<CsvFieldIdentifier, ReadOnlyMemory<char>>>)record).Count);

        IReadOnlyDictionary<CsvFieldIdentifier, ReadOnlyMemory<char>> dict = record;
        Assert.Equal(3, dict.Count);
        Assert.Equal([0, 1, 2], dict.Keys.Select(k => k.UnsafeIndex));
        Assert.Equal(["1", "2", "3"], dict.Values.Select(v => v.ToString()));
        Assert.True(dict.ContainsKey("A"));
        Assert.True(dict.TryGetValue("B", out ReadOnlyMemory<char> value));
        Assert.Equal("2", value.ToString());
        Assert.False(dict.TryGetValue(4, out _));
    }

    private static CsvPreservedRecord<char> GetRecord()
    {
        return CsvReader
            .Enumerate("1,\"Test\",true".AsMemory(), new CsvOptions<char> { HasHeader = false })
            .Preserve()
            .Single();
    }
}
