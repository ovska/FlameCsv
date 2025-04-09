using System.Buffers;
using FlameCsv.Extensions;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace FlameCsv.Tests;

public class MetricsTests
{
    [Fact]
    public static void Should_Report_Too_Large_Rent()
    {
        using var pool = new Pool();
        using var collector = new MetricCollector<short>(null, "FlameCsv", "memory.buffer_too_large");

        IMemoryOwner<char>? owner = null;

        pool.EnsureCapacity(ref owner, 128);

        Assert.Empty(collector.GetMeasurementSnapshot());

        pool.EnsureCapacity(ref owner, 129);

        var measurements = collector.GetMeasurementSnapshot();
        Assert.Single(measurements);
        Assert.Equal(1, measurements[0].Value);

        owner.Dispose();
    }

    private sealed class Pool : MemoryPool<char>
    {
        public override int MaxBufferSize => 128;

        public override IMemoryOwner<char> Rent(int minBufferSize = -1)
        {
            if (minBufferSize == -1)
                minBufferSize = 128;

            return new HeapMemoryOwner<char>(new char[minBufferSize]);
        }

        protected override void Dispose(bool disposing)
        {
        }
    }
}
