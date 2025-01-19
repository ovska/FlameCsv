using System.Runtime.CompilerServices;
using FlameCsv.Converters;

namespace FlameCsv.Benchmark;

[MemoryDiagnoser]
[HideColumns("Error", "StdDev", "Gen0")]
public class OptionsBench
{
    private static readonly CsvOptions<char> _options = new();
    private static readonly object _cacheKey = new();

    public CsvOptions<char> Options
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { return Reuse ? _options : new CsvOptions<char>(); }
    }

    [Params(true, false)] public bool Reuse { get; set; }

    [Benchmark(Baseline = true)]
    public void FromOptions()
    {
        _ = Options.GetConverter<DayOfWeek>();
    }

    [Benchmark]
    public void FromDefault()
    {
        _ = Options.GetOrCreate(static o => new EnumTextConverter<DayOfWeek>(o));
    }

    [Benchmark]
    public void FromMember()
    {
        _ = Options.GetOrCreate(_cacheKey, static o => new EnumTextConverter<DayOfWeek>(o));
    }
}
