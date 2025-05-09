using System.Reflection;
using FlameCsv.Reflection;
using FlameCsv.Utilities;

namespace FlameCsv.Binding;

internal sealed class ParameterCsvBinding<T> : CsvBinding<T>
{
    public ConstructorInfo Constructor => (ConstructorInfo)Parameter.Member;
    public ParameterInfo Parameter { get; }

    public override Type? DeclaringType => Constructor.DeclaringType;
    public override Type Type => Parameter.ParameterType;
    protected override object Sentinel => Parameter;

    protected override ReadOnlySpan<object> Attributes => _data.Attributes;

    private readonly ReflectionData _data;

    public ParameterCsvBinding(int index, ParameterInfo parameter)
        : base(index, parameter.Name)
    {
        Parameter = parameter;
        _data = ReflectionData.Empty;
    }

    public ParameterCsvBinding(int index, ParameterData parameter)
        : base(index, parameter.Value.Name)
    {
        Parameter = parameter.Value;
        _data = parameter;
    }

    private protected override void PrintDetails(ref ValueStringBuilder vsb)
    {
        vsb.Append("Parameter: ");
        vsb.Append(Type.Name);
        vsb.Append(' ');
        vsb.Append(Parameter.Name);
    }

    // ReSharper disable once StringLiteralTypo
    protected internal override string DisplayName => $"{Parameter.Name} (Parameter)";
}
