using System.Diagnostics.Metrics;

namespace FlameCsv;

internal static class Metrics
{
    public const string MeterName = "FlameCsv";

    // ReSharper disable once UnusedMember.Local
    private static readonly Meter _meter = new(MeterName);
}
