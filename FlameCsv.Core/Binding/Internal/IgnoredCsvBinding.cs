using System.Text;

namespace FlameCsv.Binding.Internal;

internal sealed class IgnoredCsvBinding<T>(int index) : CsvBinding<T>(index)
{
    public override Type Type => typeof(CsvIgnored);

    internal protected override object Sentinel => typeof(CsvIgnored);

    protected override ReadOnlySpan<object> Attributes => default;

    protected override void PrintDetails(StringBuilder sb)
    {
        sb.Append("Ignored");
    }
}
