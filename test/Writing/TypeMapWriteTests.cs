using System.Text;
using FlameCsv.Binding;
using FlameCsv.Writing;

namespace FlameCsv.Tests.Writing;

/// <summary>
/// Ensures the type map overloads use the type map instead of falling back to reflection.
/// The type map writes output that the reflection binder can never produce.
/// </summary>
public class TypeMapWriteTests
{
    public sealed class Item
    {
        public int Id { get; set; }
    }

    private sealed class MarkerTypeMap : CsvTypeMap<char, Item>
    {
        protected override IDematerializer<char, Item> BindForWriting(CsvOptions<char> options) => new Dematerializer();

        private sealed class Dematerializer : IDematerializer<char, Item>
        {
            public int FieldCount => 1;

            public void Write(CsvFieldWriter<char> writer, Item value) => writer.WriteText($"typemap-{value.Id}");

            public void WriteHeader(CsvFieldWriter<char> writer) => writer.WriteText("from_typemap");
        }
    }

    private static readonly Item[] _items = [new() { Id = 1 }, new() { Id = 2 }];
    private const string Expected = "from_typemap\r\ntypemap-1\r\ntypemap-2\r\n";

    private static async IAsyncEnumerable<Item> ItemsAsync()
    {
        foreach (var item in _items)
        {
            await Task.Yield();
            yield return item;
        }
    }

    [Fact]
    public void Write_Uses_TypeMap()
    {
        var sb = new StringBuilder();
        Csv.To(sb).Write(new MarkerTypeMap(), _items);
        Assert.Equal(Expected, sb.ToString());
    }

    [Fact]
    public async Task WriteAsync_Enumerable_Uses_TypeMap()
    {
        var sb = new StringBuilder();
        await Csv.To(sb).WriteAsync(new MarkerTypeMap(), _items, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(Expected, sb.ToString());
    }

    [Fact]
    public async Task WriteAsync_AsyncEnumerable_Uses_TypeMap()
    {
        var sb = new StringBuilder();
        await Csv.To(sb).WriteAsync(new MarkerTypeMap(), ItemsAsync(), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(Expected, sb.ToString());
    }

    [Fact]
    public void Parallel_Write_Uses_TypeMap()
    {
        var sb = new StringBuilder();
        Csv.To(sb).AsParallel(TestContext.Current.CancellationToken).Write(new MarkerTypeMap(), _items);
        Assert.Equal(Expected, sb.ToString());
    }

    [Fact]
    public async Task Parallel_WriteAsync_Enumerable_Uses_TypeMap()
    {
        var sb = new StringBuilder();
        await Csv.To(sb).AsParallel(TestContext.Current.CancellationToken).WriteAsync(new MarkerTypeMap(), _items);
        Assert.Equal(Expected, sb.ToString());
    }

    [Fact]
    public async Task Parallel_WriteAsync_AsyncEnumerable_Uses_TypeMap()
    {
        var sb = new StringBuilder();
        await Csv.To(sb).AsParallel(TestContext.Current.CancellationToken).WriteAsync(new MarkerTypeMap(), ItemsAsync());
        Assert.Equal(Expected, sb.ToString());
    }
}
