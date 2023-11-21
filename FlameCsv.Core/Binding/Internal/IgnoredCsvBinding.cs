using System.Text;

namespace FlameCsv.Binding.Internal;

internal sealed class IgnoredCsvBinding<T>(int index) : CsvBinding<T>(index)
{
    public override Type Type => typeof(object);

    internal protected override object Sentinel => Type.Missing;

    protected override ReadOnlySpan<object> Attributes => default;

    protected override void PrintDetails(StringBuilder sb)
    {
        sb.Append("Ignored");
    }
}
