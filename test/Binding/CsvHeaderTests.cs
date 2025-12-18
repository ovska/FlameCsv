namespace FlameCsv.Tests.Binding;

public static class CsvHeaderTests
{
    [Fact]
    public static void Should_Not_Pool_Huge_Value()
    {
        string data = new string('a', 512) + "\n1\n";

        CsvHeader? h1 = null;
        CsvHeader? h2 = null;

        foreach (var record in Csv.From(data).Enumerate())
        {
            h1 = record.Header;
        }

        foreach (var record in Csv.From(data).Enumerate())
        {
            h2 = record.Header;
        }

        Assert.NotNull(h1);
        Assert.NotNull(h2);
        Assert.Equal(h1, h2);
        Assert.Equal(h1.Values[0], h2.Values[0]);
        Assert.NotSame(h1.Values[0], h2.Values[0]);
        Assert.Equal(data.AsSpan(0, 512), h1.Values[0]);
    }

    [Fact]
    public static void Should_Pool()
    {
        const string data1 = "A,B,C\n1,2,3\n";

        CsvHeader? h1 = null;
        CsvHeader? h2 = null;

        foreach (var record in Csv.From(data1).Enumerate())
        {
            h1 = record.Header;
        }

        foreach (var record in Csv.From(data1).Enumerate())
        {
            h2 = record.Header;
        }

        Assert.NotNull(h1);
        Assert.NotNull(h2);
        Assert.Same(h1.Values[0], h2.Values[0]);
        Assert.Same(h1.Values[1], h2.Values[1]);
        Assert.Same(h1.Values[2], h2.Values[2]);

        // cannot be cached if normalization is used
        CsvHeader? h3 = null;
        var options = new CsvOptions<char>() { NormalizeHeader = s => s.ToString() };

        foreach (var record in Csv.From(data1).Enumerate(options))
        {
            h3 = record.Header;
        }

        Assert.NotNull(h3);
        Assert.Equal(h1, h3);
        Assert.NotSame(h1.Values[0], h3.Values[0]);
        Assert.NotSame(h1.Values[1], h3.Values[1]);
        Assert.NotSame(h1.Values[2], h3.Values[2]);
    }

    [Fact]
    public static void Should_Return_By_Name()
    {
        var header = new CsvHeader(true, ["A", "B", "C"]);

        Assert.True(header.TryGetValue("A", out int index));
        Assert.Equal(0, index);
        Assert.True(header.TryGetValue("B", out index));
        Assert.Equal(1, index);
        Assert.True(header.TryGetValue("C", out index));
        Assert.Equal(2, index);
        Assert.False(header.TryGetValue("D", out index));

        Assert.True(header.ContainsKey("A"));
        Assert.True(header.ContainsKey("B"));
        Assert.True(header.ContainsKey("C"));
        Assert.False(header.ContainsKey("D"));

        Assert.Throws<ArgumentNullException>(() => header.TryGetValue(null!, out _));
        Assert.Throws<ArgumentNullException>(() => header.ContainsKey(null!));
    }

    [Fact]
    public static void Should_Equal()
    {
        var header = new CsvHeader(true, ["A", "B", "C"]);
        var header2 = new CsvHeader(true, ["A", "B", "C"]);

        Assert.Equal(header, header2);
        Assert.True(header.Equals(header2));
        Assert.True(header.Equals((object)header2));
        Assert.True(header.GetHashCode() == header2.GetHashCode());

        var header3 = new CsvHeader(false, ["A", "B", "C"]);
        Assert.NotEqual(header, header3);
        Assert.False(header.Equals(header3));
        Assert.False(header.Equals((object)header3));
        Assert.False(header.GetHashCode() == header3.GetHashCode());

        // for code coverage
        Assert.NotNull(header.ToString());
        Assert.True(header.Equals(header));
        Assert.False(header.Equals(null));
    }
}
