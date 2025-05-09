using FlameCsv.Tests.TestData;

namespace FlameCsv.Tests.Writing;

public static class WriterEdgeCaseTests
{
    [Fact]
    public static async Task Should_Not_Flush_On_Exception()
    {
        await using (StringWriter sw = new())
        {
            Assert.Throws<InvalidDataException>(() => CsvWriter.Write(sw, InvalidData()));
            Assert.Empty(sw.ToString());
        }

        await using (StringWriter sw = new())
        {
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                CsvWriter.WriteAsync(sw, InvalidData(), cancellationToken: TestContext.Current.CancellationToken)
            );
            Assert.Empty(sw.ToString());
        }

        await using (StringWriter sw = new())
        {
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                CsvWriter.WriteAsync(sw, InvalidDataAsync(), cancellationToken: TestContext.Current.CancellationToken)
            );
            Assert.Empty(sw.ToString());
        }

        IEnumerable<Obj> InvalidData()
        {
            yield return new();
            throw new InvalidDataException();
        }

        async IAsyncEnumerable<Obj> InvalidDataAsync()
        {
            await Task.Yield();
            yield return new();
            throw new InvalidDataException();
        }
    }

    [Fact]
    public static async Task Should_Write_Header()
    {
        CsvOptions<char> options = CsvOptions<char>.Default;
        const string header = $"{TestDataGenerator.Header}\r\n";

        await using (StringWriter sw = new())
        {
            // ReSharper disable once MethodHasAsyncOverload
            CsvWriter.Write(sw, Array.Empty<Obj>(), options);
            Assert.Equal(header, sw.ToString());
        }

        await using (StringWriter sw = new())
        {
            // ReSharper disable once MethodHasAsyncOverload
            CsvWriter.Write(sw, Empty(), options);
            Assert.Equal(header, sw.ToString());
        }

        await using (StringWriter sw = new())
        {
            await CsvWriter.WriteAsync<Obj>(sw, [], options, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(header, sw.ToString());
        }

        await using (StringWriter sw = new())
        {
            await CsvWriter.WriteAsync(sw, Empty(), options, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(header, sw.ToString());
        }

        await using (StringWriter sw = new())
        {
            await CsvWriter.WriteAsync(
                sw,
                SyncAsyncEnumerable.Create(Empty()),
                options,
                cancellationToken: TestContext.Current.CancellationToken
            );
            Assert.Equal(header, sw.ToString());
        }
    }

    [Fact]
    public static async Task Should_Write_Empty_Line()
    {
        CsvOptions<char> options = new() { HasHeader = false };

        await using (StringWriter sw = new())
        {
            // ReSharper disable once MethodHasAsyncOverload
            CsvWriter.Write(sw, Array.Empty<Obj>(), options);
            Assert.Equal("\r\n", sw.ToString());
        }

        await using (StringWriter sw = new())
        {
            // ReSharper disable once MethodHasAsyncOverload
            CsvWriter.Write(sw, Empty(), options);
            Assert.Equal("\r\n", sw.ToString());
        }

        await using (StringWriter sw = new())
        {
            await CsvWriter.WriteAsync<Obj>(sw, [], options, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("\r\n", sw.ToString());
        }

        await using (StringWriter sw = new())
        {
            await CsvWriter.WriteAsync(sw, Empty(), options, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("\r\n", sw.ToString());
        }

        await using (StringWriter sw = new())
        {
            await CsvWriter.WriteAsync(
                sw,
                SyncAsyncEnumerable.Create(Empty()),
                options,
                cancellationToken: TestContext.Current.CancellationToken
            );
            Assert.Equal("\r\n", sw.ToString());
        }
    }

    private static IEnumerable<Obj> Empty()
    {
        yield break;
    }
}
