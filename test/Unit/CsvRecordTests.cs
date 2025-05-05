using System.Diagnostics.CodeAnalysis;
using FlameCsv.Attributes;
using FlameCsv.Enumeration;

namespace FlameCsv.Tests;

[SuppressMessage("ReSharper", "GenericEnumeratorNotDisposed")]
public static partial class CsvRecordTests
{
    [Fact]
    public static async Task Should_Handle_Internal_State_Async()
    {
        CsvValueRecord<char>.Enumerator secondRecordEnumerator;

        await using (
            var enumerator = CsvReader
                .Enumerate(new StringReader("A,B,C\r\n1,2,3\r\n4,5,6\r\n"))
                .GetAsyncEnumerator(TestContext.Current.CancellationToken)
        )
        {
            Assert.Equal(0, enumerator.Line);
            Assert.Equal(0, enumerator.Position);

            Assert.True(await enumerator.MoveNextAsync());

            Assert.Equal(2, enumerator.Line);
            Assert.Equal(14, enumerator.Position);

            CsvValueRecord<char> firstRecord = enumerator.Current;

            Assert.NotNull(firstRecord.Header);
            Assert.Equal("1,2,3", firstRecord.RawRecord.ToString());

            List<string> fields = [];
            foreach (var field in firstRecord)
                fields.Add(field.ToString());
            Assert.Equal(["1", "2", "3"], fields);

            var firstRecordEnumerator = firstRecord.GetEnumerator();

            Assert.True(await enumerator.MoveNextAsync());

            Assert.Equal(3, enumerator.Line);
            Assert.Equal(21, enumerator.Position);

            Assert.ThrowsAny<InvalidOperationException>(() => firstRecordEnumerator.MoveNext());
            Assert.ThrowsAny<InvalidOperationException>(() => firstRecord.GetEnumerator());

            secondRecordEnumerator = enumerator.Current.GetEnumerator();

            Assert.False(await enumerator.MoveNextAsync());

            Assert.Equal(3, enumerator.Line);
            Assert.Equal(21, enumerator.Position);
        }

        Assert.ThrowsAny<ObjectDisposedException>(() => secondRecordEnumerator.MoveNext());
    }

    [Fact]
    public static void Should_Handle_Internal_State()
    {
        CsvValueRecord<char>.Enumerator secondRecordEnumerator;

        using (var enumerator = CsvReader.Enumerate("A,B,C\r\n1,2,3\r\n4,5,6\r\n").GetEnumerator())
        {
            Assert.Equal(0, enumerator.Line);
            Assert.Equal(0, enumerator.Position);

            Assert.True(enumerator.MoveNext());

            Assert.Equal(2, enumerator.Line);
            Assert.Equal(14, enumerator.Position);

            CsvValueRecord<char> firstRecord = enumerator.Current;

            Assert.NotNull(firstRecord.Header);
            Assert.Equal("1,2,3", firstRecord.RawRecord.ToString());

            List<string> fields = [];
            foreach (var field in firstRecord)
                fields.Add(field.ToString());
            Assert.Equal(["1", "2", "3"], fields);

            var firstRecordEnumerator = firstRecord.GetEnumerator();

            Assert.True(enumerator.MoveNext());

            Assert.Equal(3, enumerator.Line);
            Assert.Equal(21, enumerator.Position);

            Assert.ThrowsAny<InvalidOperationException>(() => firstRecordEnumerator.MoveNext());
            Assert.ThrowsAny<InvalidOperationException>(() => firstRecord.GetEnumerator());

            secondRecordEnumerator = enumerator.Current.GetEnumerator();

            Assert.False(enumerator.MoveNext());

            Assert.Equal(3, enumerator.Line);
            Assert.Equal(21, enumerator.Position);
        }

        Assert.ThrowsAny<ObjectDisposedException>(() => secondRecordEnumerator.MoveNext());
    }

    [Fact]
    public static void Should_Parse_Fields_And_Records()
    {
        var enumerable = new CsvRecordEnumerable<char>("A,B,C\r\n1,2,3\r\n".AsMemory(), CsvOptions<char>.Default);

        using IEnumerator<CsvValueRecord<char>> enumerator = enumerable.GetEnumerator();

        Assert.True(enumerator.MoveNext());

        var record = enumerator.Current;

        Assert.NotNull(record.Header);
        Assert.Equal(["A", "B", "C"], record.Header?.Values);

        Assert.Equal("1,2,3", record.RawRecord.ToString());

        Assert.Equal("1", record[0].ToString());
        Assert.Equal("2", record[1].ToString());
        Assert.Equal("3", record[2].ToString());
        Assert.Equal("1", record.GetField(0).ToString());
        Assert.Equal("2", record.GetField(1).ToString());
        Assert.Equal("3", record.GetField(2).ToString());

        Assert.Equal("1", record["A"].ToString());
        Assert.Equal("2", record["B"].ToString());
        Assert.Equal("3", record["C"].ToString());
        Assert.Equal("1", record.GetField("A").ToString());
        Assert.Equal("2", record.GetField("B").ToString());
        Assert.Equal("3", record.GetField("C").ToString());

        Assert.Equal(1, record.ParseField<int>(0));
        Assert.Equal(1, record.ParseField<int>("A"));
        Assert.Equal(2, record.ParseField<int>(1));
        Assert.Equal(2, record.ParseField<int>("B"));
        Assert.Equal(3, record.ParseField<int>(2));
        Assert.Equal(3, record.ParseField<int>("C"));

        Assert.Equal(2, record.Line);
        Assert.Equal(7L, record.Position);

        Obj[] objs = [record.ParseRecord<Obj>(), record.ParseRecord(ObjTypeMap.Default)];

        foreach (var obj in objs)
        {
            Assert.Equal(1, obj.A);
            Assert.Equal(2, obj.B);
            Assert.Equal(3, obj.C);
        }

        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public static void Should_Parse_Fields_And_Records_Obj()
    {
        var enumerable = new CsvRecordEnumerable<char>(
            "A,B,C\r\n1,2,3\r\n".AsMemory(),
            CsvOptions<char>.Default
        ).Preserve();

        using IEnumerator<CsvRecord<char>> enumerator = enumerable.GetEnumerator();

        Assert.True(enumerator.MoveNext());

        var record = enumerator.Current;

        Assert.True(record.HasHeader);
        Assert.Equal(["A", "B", "C"], record.Header.Values);

        Assert.Equal("1,2,3", record.RawRecord.ToString());

        Assert.Equal("1", record[0].ToString());
        Assert.Equal("2", record[1].ToString());
        Assert.Equal("3", record[2].ToString());
        Assert.Equal("1", record.GetField(0).ToString());
        Assert.Equal("2", record.GetField(1).ToString());
        Assert.Equal("3", record.GetField(2).ToString());

        Assert.Equal("1", record["A"].ToString());
        Assert.Equal("2", record["B"].ToString());
        Assert.Equal("3", record["C"].ToString());
        Assert.Equal("1", record.GetField("A").ToString());
        Assert.Equal("2", record.GetField("B").ToString());
        Assert.Equal("3", record.GetField("C").ToString());

        Assert.Equal(1, record.ParseField<int>(0));
        Assert.Equal(1, record.ParseField<int>("A"));
        Assert.Equal(2, record.ParseField<int>(1));
        Assert.Equal(2, record.ParseField<int>("B"));
        Assert.Equal(3, record.ParseField<int>(2));
        Assert.Equal(3, record.ParseField<int>("C"));

        Assert.Equal(2, record.Line);
        Assert.Equal(7L, record.Position);

        Obj[] objs = [record.ParseRecord<Obj>(), record.ParseRecord(ObjTypeMap.Default)];

        foreach (var obj in objs)
        {
            Assert.Equal(1, obj.A);
            Assert.Equal(2, obj.B);
            Assert.Equal(3, obj.C);
        }

        Assert.False(enumerator.MoveNext());
    }

    private sealed class Obj
    {
        public int A { get; set; }
        public int B { get; set; }
        public int C { get; set; }
    }

    [CsvTypeMap<char, Obj>]
    private partial class ObjTypeMap;
}
