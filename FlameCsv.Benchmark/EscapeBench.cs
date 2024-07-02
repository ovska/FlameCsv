using FlameCsv.Writing;

namespace FlameCsv.Benchmark;

[SimpleJob]
public class EscapeBench
{
    public const string Input = "Cardinal Slant-D® Ring \"Binder\", Heavy Gauge Vinyl";

    private readonly char[] _buffer = new char[1024];

    private RFC4180Escaper<char> _escaper;
    private int specialCount;

    public bool RFC4180;

    [GlobalSetup]
    public void Setup()
    {
        _escaper = new RFC4180Escaper<char>(',', '"', '\r', '\n', 2, default);
        _escaper.NeedsEscaping(Input, out specialCount);

        ArgumentOutOfRangeException.ThrowIfZero(specialCount);
    }

    [Benchmark(Baseline = true)]
    public void Old()
    {
        ((IEscaper<char>)_escaper).EscapeField(Input, _buffer.AsSpan(0, Input.Length + 2 + specialCount), specialCount);
    }

    //[Benchmark(Baseline = false)]
    //public void New()
    //{
    //    _escaper.EscapeField2(Input, _buffer.AsSpan(0, Input.Length + 2 + specialCount));
    //}
}
