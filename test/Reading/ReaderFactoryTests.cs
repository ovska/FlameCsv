using FlameCsv.IO.Internal;

namespace FlameCsv.Tests.Reading;

public static class ReaderFactoryTests
{
    [Fact]
    public static void Should_Return_Instance()
    {
        var r = new ConstantBufferReader<byte>(Array.Empty<byte>());
        Assert.Same(r, new ReaderFactory<byte>(r).Create(true));
        Assert.Same(r, new ReaderFactory<byte>(r).Create(false));
    }

    [Fact]
    public static void Should_Use_Factory()
    {
        var a = new ConstantBufferReader<byte>(Array.Empty<byte>());
        var b = new ConstantBufferReader<byte>(Array.Empty<byte>());

        var factory = new ReaderFactory<byte>(isAsync => isAsync ? a : b);
        Assert.Same(a, factory.Create(true));
        Assert.Same(b, factory.Create(false));
    }
}
