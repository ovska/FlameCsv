using System.Buffers;
using FlameCsv.Attributes;
using FlameCsv.Tests.TestData;

namespace FlameCsv.Tests.Binding;

public partial class IgnoredFieldTests
{
    [Fact]
    public static void Should_Ignore_Index()
    {
        string data = TestDataGenerator.GenerateText(CsvNewline.CRLF, writeHeader: false, hasQuotes: true);
        Verify(Csv.From(data), header: false);
    }

    [Fact]
    public static void Should_Ignore_Header()
    {
        string data = TestDataGenerator.GenerateText(CsvNewline.CRLF, writeHeader: true, hasQuotes: true);
        Verify(Csv.From(data), header: true);
    }

    private static void Verify(Csv.IReadBuilder<char> builder, bool header)
    {
        var options = new CsvOptions<char> { HasHeader = header, IgnoreUnmatchedHeaders = true };
        var fullObjs = builder.Read<Obj>(options).Take(10).ToList();
        var partialObjs = builder.Read<PartialObj>(options).Take(10).ToList();
        var partialObjsSrcGen = builder.Read<PartialObj>(TypeMap.Default, options).Take(10).ToList();

        Assert.Equal(10, fullObjs.Count);
        Assert.Equal(fullObjs.Count, partialObjs.Count);
        Assert.Equal(fullObjs.Count, partialObjsSrcGen.Count);
        Assert.All(
            fullObjs.Zip(partialObjs, partialObjsSrcGen),
            pair =>
            {
                Assert.True(pair.Second.Equals(pair.First));
                Assert.True(pair.Second.Equals(pair.Third));
                Assert.True(pair.Third.Equals(pair.First));
            }
        );
    }

    [CsvTypeMap<char, PartialObj>]
    private partial class TypeMap;

    [CsvIgnoredIndexes(2)]
    public sealed record PartialObj : IEquatable<TestData.Obj>
    {
        [CsvIndex(0)]
        public int Id { get; set; }

        [CsvIndex(1)]
        public string? Name { get; set; }

        [CsvIgnore]
        public bool IsEnabled { get; set; }

        [CsvIndex(3)]
        public DateTimeOffset LastLogin { get; set; }

        [CsvIndex(4)]
        public Guid Token { get; set; }

        public bool Equals(Obj? other)
        {
            return other is not null
                && Id == other.Id
                && Name == other.Name
                && LastLogin == other.LastLogin
                && Token == other.Token;
        }
    }
}
