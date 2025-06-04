using System.Text;
using FlameCsv.IO;
using FlameCsv.Tests.TestData;

namespace FlameCsv.Tests.Reading;

public class FileReadingTests : IClassFixture<FileReadingTests.TestFileFixture>
{
    public class TestFileFixture : IDisposable
    {
        public string FilePath { get; } = Path.GetTempFileName();
        public string UnicodeFilePath { get; } = Path.GetTempFileName();

        public string GetPath(bool utf8) => utf8 ? FilePath : UnicodeFilePath;

        public TestFileFixture()
        {
            // Write test data to the temporary file using the same data as FileWritingTests
            var options = new CsvOptions<char> { Formats = { { typeof(DateTimeOffset), "O" } } };
            CsvWriter.WriteToFile(FilePath, GetTestData(), options, encoding: Encoding.UTF8);
            CsvWriter.WriteToFile(UnicodeFilePath, GetTestData(), options, encoding: Encoding.Unicode);
        }

        public void Dispose()
        {
            File.Delete(FilePath);
            File.Delete(UnicodeFilePath);
        }

        private static IEnumerable<Obj> GetTestData()
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

    private readonly TestFileFixture _fixture;

    public FileReadingTests(TestFileFixture fixture)
    {
        _fixture = fixture;
    }

    public static TheoryData<bool, bool> AsyncAndEncodingData =>
        new()
        {
            { false, true }, // sync, UTF8 (uses Utf8StreamReader)
            { false, false }, // sync, UTF16 (uses StreamReader)
            { true, true }, // async, UTF8 (uses Utf8StreamReader)
            { true, false }, // async, UTF16 (uses StreamReader)
        };

    [Theory, MemberData(nameof(AsyncAndEncodingData))]
    public async Task ReadFromFile_WithReflection_Char_ShouldReadCorrectly(bool isAsync, bool useUtf8)
    {
        var encoding = useUtf8 ? Encoding.UTF8 : Encoding.Unicode;
        var options = new CsvOptions<char> { Formats = { { typeof(DateTimeOffset), "O" } } };

        var results = isAsync
            ? await CsvReader
                .ReadFromFile<Obj>(_fixture.GetPath(useUtf8), options, encoding)
                .ToListAsync(TestContext.Current.CancellationToken)
            : CsvReader.ReadFromFile<Obj>(_fixture.GetPath(useUtf8), options, encoding).ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal(1, results[0].Id);
        Assert.Equal("Alice", results[0].Name);
        Assert.True(results[0].IsEnabled);
        Assert.Equal(2, results[1].Id);
        Assert.Equal("Bob", results[1].Name);
        Assert.False(results[1].IsEnabled);
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task ReadFromFile_WithReflection_Byte_ShouldReadCorrectly(bool isAsync)
    {
        var options = new CsvOptions<byte> { Formats = { { typeof(DateTimeOffset), "O" } } };

        var results = isAsync
            ? await CsvReader
                .ReadFromFile<Obj>(_fixture.FilePath, options)
                .ToListAsync(TestContext.Current.CancellationToken)
            : CsvReader.ReadFromFile<Obj>(_fixture.FilePath, options).ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal(1, results[0].Id);
        Assert.Equal("Alice", results[0].Name);
        Assert.True(results[0].IsEnabled);
        Assert.Equal(2, results[1].Id);
        Assert.Equal("Bob", results[1].Name);
        Assert.False(results[1].IsEnabled);
    }

    [Theory, MemberData(nameof(AsyncAndEncodingData))]
    public async Task ReadFromFile_WithTypeMap_Char_ShouldReadCorrectly(bool isAsync, bool useUtf8)
    {
        var encoding = useUtf8 ? Encoding.UTF8 : Encoding.Unicode;
        var options = new CsvOptions<char> { Formats = { { typeof(DateTimeOffset), "O" } } };

        var results = isAsync
            ? await CsvReader
                .ReadFromFile(_fixture.GetPath(useUtf8), ObjCharTypeMap.Default, options, encoding)
                .ToListAsync(TestContext.Current.CancellationToken)
            : CsvReader.ReadFromFile(_fixture.GetPath(useUtf8), ObjCharTypeMap.Default, options, encoding).ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal(1, results[0].Id);
        Assert.Equal("Alice", results[0].Name);
        Assert.True(results[0].IsEnabled);
        Assert.Equal(2, results[1].Id);
        Assert.Equal("Bob", results[1].Name);
        Assert.False(results[1].IsEnabled);
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task ReadFromFile_WithTypeMap_Byte_ShouldReadCorrectly(bool isAsync)
    {
        var options = new CsvOptions<byte> { Formats = { { typeof(DateTimeOffset), "O" } } };

        var results = isAsync
            ? await CsvReader
                .ReadFromFile(_fixture.FilePath, ObjByteTypeMap.Default, options)
                .ToListAsync(TestContext.Current.CancellationToken)
            : CsvReader.ReadFromFile(_fixture.FilePath, ObjByteTypeMap.Default, options).ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal(1, results[0].Id);
        Assert.Equal("Alice", results[0].Name);
        Assert.True(results[0].IsEnabled);
        Assert.Equal(2, results[1].Id);
        Assert.Equal("Bob", results[1].Name);
        Assert.False(results[1].IsEnabled);
    }

    [Theory, MemberData(nameof(AsyncAndEncodingData))]
    public async Task EnumerateFromFile_Char_ShouldReadCorrectly(bool isAsync, bool useUtf8)
    {
        var encoding = useUtf8 ? Encoding.UTF8 : Encoding.Unicode;
        var options = new CsvOptions<char>();
        var path = _fixture.GetPath(useUtf8);

        var records = new List<string[]>();

        if (isAsync)
        {
            await foreach (
                var record in CsvReader
                    .EnumerateFromFile(path, options, encoding)
                    .WithCancellation(TestContext.Current.CancellationToken)
            )
            {
                records.Add(record.ToArray());
            }
        }
        else
        {
            foreach (var record in CsvReader.EnumerateFromFile(path, options, encoding))
            {
                records.Add(record.ToArray());
            }
        }

        Assert.Equal(2, records.Count); // 2 data rows (header is not counted as a record)
        Assert.Equal(5, records[0].Length);
        Assert.Equal("1", records[0][0]);
        Assert.Equal("Alice", records[0][1]);
        Assert.Equal("true", records[0][2]);
        Assert.Equal(5, records[1].Length);
        Assert.Equal("2", records[1][0]);
        Assert.Equal("Bob", records[1][1]);
        Assert.Equal("false", records[1][2]);
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task EnumerateFromFile_Byte_ShouldReadCorrectly(bool isAsync)
    {
        var options = new CsvOptions<byte>();

        var records = new List<string[]>();

        if (isAsync)
        {
            await foreach (
                var record in CsvReader
                    .EnumerateFromFile(_fixture.FilePath, options)
                    .WithCancellation(TestContext.Current.CancellationToken)
            )
            {
                records.Add(record.ToArray());
            }
        }
        else
        {
            foreach (var record in CsvReader.EnumerateFromFile(_fixture.FilePath, options))
            {
                records.Add(record.ToArray());
            }
        }

        Assert.Equal(2, records.Count); // 2 data rows (header is not counted as a record)
        Assert.Equal(5, records[0].Length);
        Assert.Equal("1", records[0][0]);
        Assert.Equal("Alice", records[0][1]);
        Assert.Equal("true", records[0][2]);
        Assert.Equal(5, records[1].Length);
        Assert.Equal("2", records[1][0]);
        Assert.Equal("Bob", records[1][1]);
        Assert.Equal("false", records[1][2]);
    }

    [Fact]
    public void ReadFromFile_WithCustomIOOptions_ShouldWork()
    {
        var options = new CsvOptions<char> { Formats = { { typeof(DateTimeOffset), "O" } } };
        var ioOptions = new CsvIOOptions { BufferSize = 1024, MinimumReadSize = 512 };

        var results = CsvReader.ReadFromFile<Obj>(_fixture.FilePath, options, Encoding.UTF8, ioOptions).ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal(1, results[0].Id);
        Assert.Equal("Alice", results[0].Name);
    }

    [Fact]
    public void ReadFromFile_WithNullEncoding_ShouldUseUtf8Path()
    {
        var options = new CsvOptions<char> { Formats = { { typeof(DateTimeOffset), "O" } } };

        // null encoding should use the UTF8 code path
        var results = CsvReader.ReadFromFile<Obj>(_fixture.FilePath, options, encoding: null).ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal(1, results[0].Id);
        Assert.Equal("Alice", results[0].Name);
    }

    [Fact]
    public void ReadFromFile_WithAsciiEncoding_ShouldUseUtf8Path()
    {
        var options = new CsvOptions<char> { Formats = { { typeof(DateTimeOffset), "O" } } };

        // ASCII encoding should use the UTF8 code path
        var results = CsvReader.ReadFromFile<Obj>(_fixture.FilePath, options, Encoding.ASCII).ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal(1, results[0].Id);
        Assert.Equal("Alice", results[0].Name);
    }

    [Theory]
    [InlineData(256)] // Small buffer
    [InlineData(4096)] // Default buffer
    [InlineData(65536)] // Large buffer
    public void ReadFromFile_WithDifferentBufferSizes_ShouldWork(int bufferSize)
    {
        var options = new CsvOptions<char> { Formats = { { typeof(DateTimeOffset), "O" } } };
        var ioOptions = new CsvIOOptions { BufferSize = bufferSize };

        var results = CsvReader.ReadFromFile<Obj>(_fixture.FilePath, options, Encoding.UTF8, ioOptions).ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal(1, results[0].Id);
        Assert.Equal("Alice", results[0].Name);
    }

    [Fact]
    public void ReadFromFile_NonExistentFile_ShouldThrow()
    {
        var options = new CsvOptions<char>();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        Assert.Throws<FileNotFoundException>(() =>
        {
            CsvReader.ReadFromFile<Obj>(nonExistentPath, options).ToList();
        });
    }

    [Fact]
    public void ReadFromFile_EmptyPath_ShouldThrow()
    {
        var options = new CsvOptions<char>();

        Assert.Throws<ArgumentException>(() =>
        {
            CsvReader.ReadFromFile<Obj>("", options).ToList();
        });
    }

    [Fact]
    public void ReadFromFile_NullPath_ShouldThrow()
    {
        var options = new CsvOptions<char>();

        Assert.Throws<ArgumentNullException>(() =>
        {
            CsvReader.ReadFromFile<Obj>(null!, options).ToList();
        });
    }
}
