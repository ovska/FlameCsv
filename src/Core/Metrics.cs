using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

namespace FlameCsv;

[ExcludeFromCodeCoverage]
internal static class Metrics
{
    public const string MeterName = "FlameCsv";

    // ReSharper disable once UnusedMember.Local
    private static readonly Meter _meter = new(MeterName);
}
