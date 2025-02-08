using System.Reflection;

namespace FlameCsv.Reflection;

internal sealed class ParameterData(ParameterInfo parameter) : ReflectionData
{
    public ParameterInfo Value => parameter;
    public override ReadOnlySpan<object> Attributes => _attributes ??= parameter.GetCustomAttributes(inherit: true);

    public bool HasDefaultValue
    {
        get
        {
            if (parameter.HasDefaultValue)
            {
                return true;
            }

#if false // TODO: Uncomment when DefaultValueAttribute is supported
            foreach (var attr in Attributes)
            {
                if (attr is DefaultValueAttribute)
                {
                    return true;
                }
            }
#endif

            return false;
        }
    }

    private object[]? _attributes;

    public static explicit operator ParameterData(ParameterInfo parameter) => new(parameter);
}
