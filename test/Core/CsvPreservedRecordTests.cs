using System.Diagnostics.CodeAnalysis;
using FlameCsv.Attributes;

namespace FlameCsv.Tests;

[SuppressMessage("ReSharper", "GenericEnumeratorNotDisposed")]
public static partial class CsvPreservedRecordTests
{
    [Fact]
    public static async Task Should_Handle_Internal_State_Async()
    {
        CsvRecord<char>.Enumerator secondRecordEnumerator;

        await using (
            var enumerator = Csv.From(new StringReader("A,B,C\r\n1,2,3\r\n4,5,6\r\n"))
                .EnumerateAsync()
                .GetAsyncEnumerator(TestContext.Current.CancellationToken)
        )
        {
            Assert.True(await enumerator.MoveNextAsync());

            Assert.Equal(2, enumerator.Line);
            Assert.Equal(2, enumerator.Current.LineNumber);
            Assert.Equal("A,B,C\r\n".Length, enumerator.Current.Position);

            CsvRecord<char> firstRecord = enumerator.Current;

            Assert.NotNull(firstRecord.Header);
            Assert.Equal("1,2,3", firstRecord.Raw.ToString());

            List<string> fields = [];
            foreach (var field in firstRecord)
                fields.Add(field.ToString());
            Assert.Equal(["1", "2", "3"], fields);

            var firstRecordEnumerator = firstRecord.GetEnumerator();
            firstRecordEnumerator.MoveNext();

            Assert.True(await enumerator.MoveNextAsync());

            Assert.Equal(3, enumerator.Line);
            Assert.Equal(3, enumerator.Current.LineNumber);
            Assert.Equal("A,B,C\r\n1,2,3\r\n".Length, enumerator.Current.Position);

            Assert.ThrowsAny<InvalidOperationException>(() => _ = firstRecordEnumerator.Current);
            Assert.ThrowsAny<InvalidOperationException>(() => firstRecord.GetEnumerator());

            secondRecordEnumerator = enumerator.Current.GetEnumerator();
            secondRecordEnumerator.MoveNext();

            Assert.False(await enumerator.MoveNextAsync());

            Assert.Equal(3, enumerator.Line);
            // Assert.Equal(21, enumerator.Position);
        }

        Assert.ThrowsAny<ObjectDisposedException>(() => _ = secondRecordEnumerator.Current);
    }

    [Fact]
    public static void Should_Handle_Internal_State()
    {
        CsvRecord<char>.Enumerator secondRecordEnumerator;

        using (var enumerator = Csv.From("A,B,C\r\n1,2,3\r\n4,5,6\r\n").Enumerate().GetEnumerator())
        {
            Assert.Equal(0, enumerator.Line);

            Assert.True(enumerator.MoveNext());

            Assert.Equal(2, enumerator.Line);
            Assert.Equal(2, enumerator.Current.LineNumber);
            Assert.Equal("A,B,C\r\n".Length, enumerator.Current.Position);

            CsvRecord<char> firstRecord = enumerator.Current;

            Assert.NotNull(firstRecord.Header);
            Assert.Equal("1,2,3", firstRecord.Raw.ToString());

            List<string> fields = [];
            foreach (var field in firstRecord)
                fields.Add(field.ToString());
            Assert.Equal(["1", "2", "3"], fields);

            var firstRecordEnumerator = firstRecord.GetEnumerator();
            firstRecordEnumerator.MoveNext();

            Assert.True(enumerator.MoveNext());

            Assert.Equal(3, enumerator.Line);
            Assert.Equal(3, enumerator.Current.LineNumber);
            Assert.Equal("A,B,C\r\n1,2,3\r\n".Length, enumerator.Current.Position);

            Assert.ThrowsAny<InvalidOperationException>(() => _ = firstRecordEnumerator.Current);
            Assert.ThrowsAny<InvalidOperationException>(() => firstRecord.GetEnumerator());

            secondRecordEnumerator = enumerator.Current.GetEnumerator();
            secondRecordEnumerator.MoveNext();

            Assert.False(enumerator.MoveNext());

            Assert.Equal(3, enumerator.Line);
            // Assert.Equal(21, enumerator.Position);
        }

        Assert.ThrowsAny<ObjectDisposedException>(() => _ = secondRecordEnumerator.Current);
    }

    [Fact]
    public static void Should_Parse_Fields_And_Records()
    {
        var enumerable = Csv.From("A,B,C\r\n1,2,3\r\n").Enumerate();

        using IEnumerator<CsvRecord<char>> enumerator = enumerable.GetEnumerator();

        Assert.True(enumerator.MoveNext());

        var record = enumerator.Current;

        Assert.NotNull(record.Header);
        Assert.Equal(["A", "B", "C"], record.Header?.Values);

        Assert.Equal("1,2,3", record.Raw.ToString());

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

        Assert.Equal(2, record.LineNumber);
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
