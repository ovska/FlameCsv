using System.Buffers;
using System.Collections;
using System.Globalization;
using FlameCsv.Attributes;
using FlameCsv.Binding;
using FlameCsv.Converters.Formattable;
using FlameCsv.Enumeration;
using FlameCsv.Exceptions;
using FlameCsv.IO;
using JetBrains.Annotations;

namespace FlameCsv.Tests.Reading;

public sealed class CsvEnumerationTests : IDisposable
{
    private class Shim
    {
        [CsvIndex(0)]
        public int Id { get; set; }

        [CsvIndex(1)]
        public string? Name { get; set; }
    }

    [Fact]
    public void Should_Reset()
    {
        using var resetable = CsvReader.Enumerate("1,2,3\n", new() { HasHeader = false }).GetEnumerator();

        Assert.True(resetable.MoveNext());
        Assert.Equal("1,2,3", resetable.Current.RawRecord.ToString());
        Assert.NotEqual(0, resetable.Position);
        Assert.Equal(1, resetable.Line);

        ((IEnumerator)resetable).Reset();
        Assert.Equal(0, resetable.Position);
        Assert.Equal(0, resetable.Line);

        Assert.True(resetable.MoveNext());
        Assert.Equal("1,2,3", resetable.Current.RawRecord.ToString());
        Assert.NotEqual(0, resetable.Position);
        Assert.Equal(1, resetable.Line);

        Assert.False(resetable.MoveNext());

        // StreamReader cannot be reset/rewound
        Assert.Throws<NotSupportedException>(() =>
        {
            using var enumerator = CsvReader.Enumerate(new StreamReader(Stream.Null)).GetEnumerator();
            (enumerator as IEnumerator).Reset();
        });
    }

    [Fact]
    public void Should_Validate_Field_Count()
    {
        const string data = "id,name,age\n1,Bob,30\n2,Alice,45\n3,Charlie,25,true\n";

        using var enumerator = new CsvRecordEnumerator<char>(
            new CsvOptions<char> { ValidateFieldCount = true },
            CsvBufferReader.Create(data)
        );

        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());
        Assert.Throws<CsvReadException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void Should_Reset_Header()
    {
        const string data = "id,name\r\n" + "1,Bob\r\n" + "\r\n" + "name,id\r\n" + "Alice,2\r\n";

        using var enumerator = new CsvRecordEnumerator<char>(CsvOptions<char>.Default, CsvBufferReader.Create(data));

        Assert.True(enumerator.MoveNext());
        var record1 = enumerator.Current.ParseRecord<Shim>();
        Assert.Equal(1, record1.Id);
        Assert.Equal("Bob", record1.Name);

        var currentHeader = enumerator.Header!;
        var copy = new CsvHeader(currentHeader);
        enumerator.Header = copy;
        Assert.Equal(currentHeader, enumerator.Header);

        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.Current.RawRecord.IsEmpty);
        enumerator.Header = null;

        Assert.True(enumerator.MoveNext());
        var record2 = enumerator.Current.ParseRecord<Shim>();
        Assert.Equal(2, record2.Id);
        Assert.Equal("Alice", record2.Name);

        enumerator.Dispose();
        Assert.Throws<ObjectDisposedException>(() => enumerator.Header = null);
    }

    [Fact]
    public void Should_Enumerate_Lines()
    {
        const string data = "1,\"Test\",true\r\n2,\"Asd\",false\r\n";

        using var enumerator = new CsvRecordEnumerator<char>(
            new CsvOptions<char> { HasHeader = false },
            CsvBufferReader.Create(data)
        );

        Assert.True(enumerator.MoveNext());
        Assert.NotNull(((IEnumerator)enumerator).Current);
        Assert.Equal("1,\"Test\",true", enumerator.Current.RawRecord.ToString());
        Assert.Equal(1, enumerator.Current.ParseField<int>(0));
        Assert.Equal("Test", enumerator.Current.ParseField<string>(1));
        Assert.True(enumerator.Current.ParseField<bool>(2));
        Assert.Throws<CsvParseException>(() => enumerator.Current.ParseField<int>(1));
        Assert.Throws<CsvParseException>(() => ((CsvPreservedRecord<char>)enumerator.Current).ParseField<int>(1));
        Assert.Throws<CsvParseException>(() =>
            enumerator.Current.ParseField<int>(new SpanTextConverter<int>(CsvOptions<char>.Default), 1)
        );
        Assert.Throws<CsvParseException>(() =>
            ((CsvPreservedRecord<char>)enumerator.Current).ParseField<int>(
                new SpanTextConverter<int>(CsvOptions<char>.Default),
                1
            )
        );

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

        foreach (
            var record in new CsvRecordEnumerable<char>(data.AsMemory(), new CsvOptions<char> { HasHeader = false })
        )
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
        Assert.Throws<ArgumentNullException>(() => new CsvRecordEnumerable<char>(default(ReadOnlySequence<char>), null!)
        );
    }

    [Fact]
    public void Should_Return_Field_Count()
    {
        CsvRecord<char> record = GetRecord();

        Assert.Equal(3, record.FieldCount);
    }

    [Fact]
    public void Should_Enumerate_Record()
    {
        CsvRecord<char> record = GetRecord();

        var actual = new List<string>();

        foreach (var field in record)
            actual.Add(field.ToString());

        Assert.Equal(["1", "Test", "true"], actual);
    }

    [Fact]
    public void Should_Parse_Fields()
    {
        CsvRecord<char> record = GetRecord();

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
            record.TryParseField(new NumberTextConverter<int>(record.Options, NumberStyles.Integer), 0, out _1)
        );
        Assert.Equal(1, _1);
    }

    [Fact]
    public void Should_Return_Fields()
    {
        CsvRecord<char> record = GetRecord();

        Assert.Equal("1", record[0]);
        Assert.Equal("Test", record[1]);
        Assert.Equal("true", record[2]);

        Assert.Equal("1", record.GetField(0));
        Assert.Equal("Test", record.GetField(1));
        Assert.Equal("true", record.GetField(2));
    }

    [Fact]
    public void Should_Return_Fields_By_Name()
    {
        using var enumerator = new CsvRecordEnumerable<char>(
            "A,B,C\r\n1,2,3".AsMemory(),
            new CsvOptions<char> { HasHeader = true }
        ).GetEnumerator();

        Assert.True(enumerator.MoveNext());
        CsvRecord<char> record = enumerator.Current;

        Assert.Equal("1", record.GetField("A").ToString());
        Assert.Equal("2", record.GetField("B").ToString());
        Assert.Equal("3", record.GetField("C").ToString());
        Assert.Throws<ArgumentException>(() => record.GetField("D"));
        Assert.Throws<ArgumentException>(() => ((CsvPreservedRecord<char>)record).GetField("D"));

        Assert.Equal("1", record["A"].ToString());
        Assert.Equal("2", record["B"].ToString());
        Assert.Equal("3", record["C"].ToString());

        Assert.Equal("1", record[0].ToString());
        Assert.Equal("2", record[1].ToString());
        Assert.Equal("3", record[2].ToString());
        Assert.Throws<ArgumentOutOfRangeException>(() => record[3].ToString());
        Assert.Throws<ArgumentOutOfRangeException>(() => ((CsvPreservedRecord<char>)record)[3].ToString());

        Assert.True(record.Contains(0));
        Assert.True(record.Contains(1));
        Assert.True(record.Contains(2));
        Assert.False(record.Contains(3));

        Assert.True(record.Contains("A"));
        Assert.True(record.Contains("B"));
        Assert.True(record.Contains("C"));
        Assert.False(record.Contains("D"));

        var preserved = (CsvPreservedRecord<char>)record;
        Assert.True(preserved.Contains(0));
        Assert.True(preserved.Contains(1));
        Assert.True(preserved.Contains(2));
        Assert.False(preserved.Contains(3));
        Assert.True(preserved.Contains("A"));
        Assert.True(preserved.Contains("B"));
        Assert.True(preserved.Contains("C"));
        Assert.False(preserved.Contains("D"));

        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void Should_Not_Contain_Header_If_Headerless()
    {
        foreach (var record in CsvReader.Enumerate("1,2,3\r\n4,5,6", new CsvOptions<char> { HasHeader = false }))
        {
            Assert.Null(record.Header);
            Assert.False(record.Contains("A"));

            var preserved = (CsvPreservedRecord<char>)record;
            Assert.Null(preserved.Header);
            Assert.False(preserved.Contains("A"));
        }
    }

    [Fact]
    public void Should_Throw_If_No_Header()
    {
        Assert.Throws<NotSupportedException>(() => GetRecord().ParseField<int>("A"));
        Assert.Throws<NotSupportedException>(() => GetRecord().ParseField(CsvIgnored.Converter<char, object>(), "A"));

        Assert.Throws<NotSupportedException>(() =>
        {
            using var enumerator = new CsvRecordEnumerable<char>(
                "1,2,3".AsMemory(),
                new CsvOptions<char> { HasHeader = false }
            ).GetEnumerator();

            enumerator.MoveNext();
            enumerator.Header = new(StringComparer.Ordinal, ["A", "B", "C"]);
        });
    }

    [Fact]
    public void Should_Reset_On_Invalid_Access()
    {
        using var enumerator = CsvReader.Enumerate("1,Test,true\r\n2,Asd,false\r\n").GetEnumerator();

        Assert.Throws<InvalidOperationException>(() => enumerator.Current);
        Assert.Throws<InvalidOperationException>(() => ((IEnumerator)enumerator).Current);
        enumerator.Dispose();
        Assert.Throws<ObjectDisposedException>(() => enumerator.Current);
        Assert.Throws<ObjectDisposedException>(() => ((IEnumerator)enumerator).Current);
    }

    private CsvRecord<char> GetRecord()
    {
        _enumerator?.Dispose();
        _enumerator = new CsvRecordEnumerator<char>(
            new CsvOptions<char> { HasHeader = false },
            CsvBufferReader.Create("1,\"Test\",true")
        );
        _enumerator.MoveNext();
        return _enumerator.Current;
    }

    [HandlesResourceDisposal]
    private CsvRecordEnumerator<char>? _enumerator;

    public void Dispose() => _enumerator?.Dispose();
}
