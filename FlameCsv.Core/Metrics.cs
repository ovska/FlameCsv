using System.Buffers;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;

namespace FlameCsv;

internal static class Metrics
{
    public const string MeterName = "FlameCsv";
    private static readonly Meter _meter = new(MeterName);

    private static readonly Counter<short> _tooLargeRent = _meter.CreateCounter<short>(
        "memory.buffer_too_large",
        description: "Required size for a temporary buffer was larger than the configured MemoryPool's maximum");

    public static void TooLargeRent<T>(
        int requiredSize,
        MemoryPool<T> pool,
        [CallerMemberName] string callsite = "")
    {
        // we end up boxing some value types, so check if the counter is enabled first
        if (_tooLargeRent.Enabled)
        {
            _tooLargeRent.Add(
                delta: 1,
                new("required_size", requiredSize),
                new("pool.type", pool.GetType().FullName),
                new("pool.maximum", pool.MaxBufferSize),
                new("callsite", callsite));
        }
    }

    public static readonly Counter<long> parseAnyQuote = _meter.CreateCounter<long>(
        "csv.parse_any_quote");

    public static readonly Counter<long> parseAnyNewlineDelimiter = _meter.CreateCounter<long>(
        "csv.parse_any_newline_delimiter");
}
