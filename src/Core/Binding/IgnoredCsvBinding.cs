using FlameCsv.Utilities;

namespace FlameCsv.Binding;

internal sealed class IgnoredCsvBinding<T>(int index) : CsvBinding<T>(index, null)
{
    public override Type Type => typeof(CsvIgnored);
    public override Type? DeclaringType => null;

    protected override object Sentinel => typeof(CsvIgnored);

    protected override ReadOnlySpan<object> Attributes => [];

    private protected override void PrintDetails(ref ValueStringBuilder vsb)
    {
        vsb.Append("Ignored");
    }

    protected internal override string DisplayName => "Ignored";
}
