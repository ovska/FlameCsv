using FlameCsv.IO;

namespace FlameCsv.Tests.IO;

public class IOOptionsTests
{
    [Fact]
    public void Should_Return_BufferSize()
    {
        var opts = new CsvIOOptions();
        Assert.Equal(CsvIOOptions.DefaultBufferSize, opts.BufferSize);
        Assert.Equal(CsvIOOptions.DefaultMinimumReadSize, opts.MinimumReadSize);
        Assert.False(opts.HasCustomBufferSize);

        opts = opts with { BufferSize = 8192 };
        Assert.Equal(8192, opts.BufferSize);
        Assert.Equal(CsvIOOptions.DefaultMinimumReadSize, opts.MinimumReadSize);
        Assert.True(opts.HasCustomBufferSize);

        opts = opts with { BufferSize = 5 };
        Assert.Equal(CsvIOOptions.MinimumBufferSize, opts.BufferSize);
        Assert.Equal(CsvIOOptions.MinimumBufferSize / 2, opts.MinimumReadSize);

        opts = opts with { BufferSize = 2048, MinimumReadSize = 2048 };
        Assert.Equal(2048, opts.BufferSize);
        Assert.Equal(1024, opts.MinimumReadSize); // clamped to half of buffer size

        opts = opts with { BufferSize = 8096 };
        Assert.Equal(8096, opts.BufferSize);
        Assert.Equal(2048, opts.MinimumReadSize); // user set value remains

        Assert.Throws<ArgumentOutOfRangeException>(() => new CsvIOOptions { BufferSize = -2 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new CsvIOOptions { MinimumReadSize = -2 });

        Assert.Equal(CsvIOOptions.DefaultBufferSize, new CsvIOOptions { BufferSize = -1 }.BufferSize);
        Assert.Equal(CsvIOOptions.DefaultMinimumReadSize, new CsvIOOptions { MinimumReadSize = -1 }.MinimumReadSize);

        var forFiles = new CsvIOOptions { LeaveOpen = true }.ForFileIO();
        Assert.False(forFiles.LeaveOpen); // overrides
        Assert.Equal(CsvIOOptions.DefaultFileBufferSize, forFiles.BufferSize); // different default

        forFiles = new CsvIOOptions { BufferSize = 1024 }.ForFileIO();
        Assert.Equal(1024, forFiles.BufferSize); // user override
    }
}
