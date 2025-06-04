using System.Text;
using FlameCsv.Tests.TestData;

namespace FlameCsv.Tests.Writing;

public class FileWritingTests : IDisposable
{
    private string Path { get; } = System.IO.Path.GetTempFileName();

    public void Dispose()
    {
        File.Delete(Path);
    }

    private static string Expected =>
        "Id,Name,IsEnabled,LastLogin,Token\r\n"
        + "1,Alice,true,1970-01-01T00:00:00.0000000+00:00,abcdefab-abcd-abcd-abcd-abcdefabcdef\r\n"
        + "2,Bob,false,1969-12-31T00:00:00.0000000+00:00,12345678-1234-1234-1234-123456789012\r\n";

    public static TheoryData<bool, bool> Encodings => [(true, true), (false, true), (true, false), (false, false)];

    [Theory, MemberData(nameof(Encodings))]
    public async Task Should_Write_To_File_Chars(bool isAsync, bool utf8)
    {
        // utf8/ascii encodings use a slightly different code path
        var encoding = utf8 ? Encoding.UTF8 : Encoding.Unicode;
        var options = new CsvOptions<char> { Formats = { { typeof(DateTimeOffset), "O" } } };

        if (isAsync)
        {
            await CsvWriter.WriteToFileAsync(
                Path,
                Data(),
                options,
                encoding: encoding,
                cancellationToken: TestContext.Current.CancellationToken
            );
        }
        else
        {
            CsvWriter.WriteToFile(Path, Data(), options, encoding: encoding);
        }

        Assert.Equal(Expected, await File.ReadAllTextAsync(Path, encoding, TestContext.Current.CancellationToken));
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task Should_Write_To_File_Utf8(bool isAsync)
    {
        var options = new CsvOptions<byte> { Formats = { { typeof(DateTimeOffset), "O" } } };

        if (isAsync)
        {
            await CsvWriter.WriteToFileAsync(
                Path,
                Data(),
                options,
                cancellationToken: TestContext.Current.CancellationToken
            );
        }
        else
        {
            CsvWriter.WriteToFile(Path, Data(), options);
        }

        Assert.Equal(Expected, await File.ReadAllTextAsync(Path, Encoding.UTF8, TestContext.Current.CancellationToken));
    }

    private static IEnumerable<Obj> Data()
    {
        yield return new Obj
        {
            Id = 1,
            Name = "Alice",
            IsEnabled = true,
            LastLogin = DateTimeOffset.UnixEpoch,
            Token = Guid.Parse("abcdefab-abcd-abcd-abcd-abcdefabcdef"),
        };
        yield return new Obj
        {
            Id = 2,
            Name = "Bob",
            IsEnabled = false,
            LastLogin = DateTimeOffset.UnixEpoch.AddDays(-1),
            Token = Guid.Parse("12345678-1234-1234-1234-123456789012"),
        };
    }
}
