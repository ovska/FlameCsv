using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace FlameCsv.Extensions;

internal sealed class ConstructorMetadata
{
    public ConstructorInfo Constructor { get; }
    public ReadOnlySpan<object> Attributes => _attributes ?? InitAttributes();
    public ReadOnlySpan<ParameterInfo> Parameters => _parameters ?? InitParams();
    public ReadOnlySpan<object[]> ParameterAttributes => _parameterAttributes ?? InitParamAttributes();

    private object[]? _attributes;
    private ParameterInfo[]? _parameters;
    private object[][]? _parameterAttributes;

    internal ConstructorMetadata(ConstructorInfo ctor)
    {
        Constructor = ctor;
    }

    [MemberNotNull(nameof(_attributes))]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private object[] InitAttributes()
    {
        var attributes = Constructor.GetCustomAttributes(inherit: false);
        return Interlocked.CompareExchange(ref _attributes, attributes, null) ?? attributes;
    }

    [MemberNotNull(nameof(_parameters))]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private ParameterInfo[] InitParams()
    {
        var parameters = Constructor.GetParameters().ForCache();
        return Interlocked.CompareExchange(ref _parameters, parameters, null) ?? parameters;
    }

    [MemberNotNull(nameof(_parameterAttributes))]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private object[][] InitParamAttributes()
    {
        var parameters = Parameters;
        object[][] value = new object[parameters.Length][];

        for (int i = 0; i < parameters.Length; i++)
        {
            value[i] = parameters[i].GetCustomAttributes(inherit: false);
        }

        return Interlocked.CompareExchange(ref _parameterAttributes, value, null) ?? value;
    }
}
