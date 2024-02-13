using System.Reflection;

namespace FlameCsv.Reflection;

internal sealed class ParameterData(ParameterInfo parameter) : ReflectionData
{
    public ParameterInfo Value => parameter;
    public override ReadOnlySpan<object> Attributes => _attributes ??= parameter.GetCustomAttributes(inherit: true);

    private object[]? _attributes;

    public static explicit operator ParameterData(ParameterInfo parameter) => new(parameter);
}
