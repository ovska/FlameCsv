using System.Reflection;
using System.Text;
using FlameCsv.Reflection;

namespace FlameCsv.Binding;

internal sealed class ParameterCsvBinding<T> : CsvBinding<T>
{
    public ConstructorInfo Constructor => (ConstructorInfo)Parameter.Member;
    public ParameterInfo Parameter { get; }

    public override Type? DeclaringType => Constructor.DeclaringType;
    public override Type Type => Parameter.ParameterType;
    protected override object Sentinel => Parameter;

    protected override ReadOnlySpan<object> Attributes => _parameterData is not null ? _parameterData.Attributes : default;

    private readonly ParameterData? _parameterData;

    public ParameterCsvBinding(int index, ParameterInfo parameter) : base(index, parameter.Name)
    {
        Parameter = parameter;
    }

    public ParameterCsvBinding(int index, ParameterData parameter) : base(index, parameter.Value.Name)
    {
        Parameter = parameter.Value;
        _parameterData = parameter;
    }

    protected override void PrintDetails(StringBuilder sb)
    {
        sb.Append("Parameter: ");
        sb.Append(Type.Name);
        sb.Append(' ');
        sb.Append(Parameter.Name);
    }

    // ReSharper disable once StringLiteralTypo
    protected internal override string DisplayName => _displayName ??= $"{Parameter.Name} (Parameter)";

    private string? _displayName;
}
