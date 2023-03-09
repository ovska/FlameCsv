using System.Reflection;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;

namespace FlameCsv.Reflection;

internal sealed class ParameterData
{
    public ParameterInfo Value { get; }
    public ReadOnlySpan<object> Attributes => _attributes ?? GetOrInitMemberAttributes();

    private object[]? _attributes;

    private ParameterData(ParameterInfo parameter)
    {
        Value = parameter;
    }


    [MethodImpl(MethodImplOptions.NoInlining)]
    private object[] GetOrInitMemberAttributes()
    {
        var attributes = Value.GetCustomAttributes(inherit: true).ForCache();
        return Interlocked.CompareExchange(ref _attributes, attributes, null) ?? attributes;
    }

    public static explicit operator ParameterData(ParameterInfo parameter) => new(parameter);
}
