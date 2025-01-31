using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using FlameCsv.Binding.Attributes;
using FlameCsv.Exceptions;
using FlameCsv.Utilities;
using DAMT = System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes;

namespace FlameCsv.Reflection;

internal class CsvTypeInfo
{
    protected CsvTypeInfo([DAM(Messages.ReflectionBound)] Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        Type = type;

        HotReloadService.RegisterForHotReload(
            this,
            static state =>
            {
                var @this = (CsvTypeInfo)state;
                @this._customAttributes = null;
                @this._members = null;
                @this._ctors = null;
                @this._ctorParams = null;
                @this._proxyType = null;
                @this._proxyInfo = null;
            });
    }

    [DAM(Messages.ReflectionBound)] public Type Type { get; }

    private object[]? _customAttributes;
    private MemberData[]? _members;
    private CtorData[]? _ctors;
    private object? _ctorParams;

    [DAM(Messages.ReflectionBound)] private Type? _proxyType;
    private CsvTypeInfo? _proxyInfo;

    public readonly record struct CtorData(ConstructorInfo Value, ParameterData[] Params);

    public ReadOnlySpan<MemberData> Members => _members ??= InitPropertiesAndFields(Type);

    public ReadOnlySpan<object> Attributes => _customAttributes ??= InitCustomAttributes(Type);

    public ReadOnlySpan<CtorData> PublicConstructors => _ctors ??= InitConstructors(Type);

    public ReadOnlySpan<ParameterData> ConstructorParameters
        => (_ctorParams ??= InitPrimaryCtorParams()) as ParameterData[] ??
            ThrowExceptionForNoPrimaryConstructor(Type);

    public CsvTypeInfo ProxyOrSelf => Proxy ?? this;

    public CsvTypeInfo? Proxy
    {
        get
        {
            if (_proxyType is null)
            {
                if (TryGetTypeProxy(out var proxy))
                {
                    _proxyType = proxy;
                    _proxyInfo = new CsvTypeInfo(proxy);
                }
                else
                {
                    _proxyType = Type; // Cache the fact that we checked for a proxy
                }
            }

            return _proxyInfo;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MemberData GetPropertyOrField(string memberName)
    {
        foreach (var member in Members)
        {
            if (memberName == member.Value.Name)
            {
                return member;
            }
        }

        return (MemberData)ThrowMemberNotFound(Type, memberName, isParameter: false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ParameterData GetParameter(string parameterName)
    {
        foreach (var parameter in ConstructorParameters)
        {
            if (parameterName == parameter.Value.Name)
            {
                return parameter;
            }
        }

        return (ParameterData)ThrowMemberNotFound(Type, parameterName, isParameter: false);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static MemberData[] InitPropertiesAndFields([DAM(DAMT.PublicProperties | DAMT.PublicFields)] Type type)
    {
        return type
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Concat<MemberInfo>(type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            .Select(static m => (MemberData)m)
            .ToArray();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object[] InitCustomAttributes(Type type) => type.GetCustomAttributes(inherit: true);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryGetTypeProxy([DAM(Messages.ReflectionBound)][NotNullWhen(true)] out Type? typeProxy)
    {
        if (!Type.IsValueType)
        {
            foreach (var attribute in Attributes)
            {
                if (attribute is CsvTypeAttribute { CreatedTypeProxy: { } proxy })
                {
                    if (!proxy.IsAssignableTo(Type))
                    {
                        throw new CsvBindingException(
                            Type,
                            $"Invalid type proxy for {Type}: Not assignable to {proxy.FullName}).");
                    }

                    if (proxy.IsInterface)
                    {
                        throw new CsvBindingException(
                            Type,
                            $"Invalid type proxy for {Type}: Interface type {proxy.FullName} is not supported.");
                    }

                    typeProxy = proxy;
                    return true;
                }
            }
        }

        typeProxy = null;
        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private object InitPrimaryCtorParams()
    {
        var ctors = PublicConstructors;

        if (ctors.Length == 0)
        {
            return Array.Empty<ParameterData>();
        }

        if (ctors.Length == 1)
        {
            return ctors[0].Params;
        }

        ConstructorInfo? parameterlessCtor = null;

        foreach ((ConstructorInfo ctor, ParameterData[] parameters) in ctors)
        {
            foreach (var attribute in ctor.GetCustomAttributes(inherit: false))
            {
                if (attribute is CsvConstructorAttribute)
                    return parameters;
            }

            if (parameters.Length == 0)
            {
                parameterlessCtor = ctor;
            }
        }

        // No explicit ctor found, but found parameterless
        return parameterlessCtor is not null
            ? Array.Empty<ParameterData>()
            : Type.Missing;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static CtorData[] InitConstructors([DAM(DAMT.PublicConstructors)] Type type)
        => type
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public)
            .Select(c => new CtorData(c, c.GetParameters().Select(p => new ParameterData(p)).ToArray()))
            .ToArray();

    private static ParameterData[] ThrowExceptionForNoPrimaryConstructor(Type type)
    {
        throw new CsvBindingException(
            type,
            $"No empty constructor or constructor with [CsvConstructor] found for type {type.FullName}");
    }

    private static object ThrowMemberNotFound(Type type, string memberName, bool isParameter)
    {
        throw new CsvConfigurationException(
            $"{(isParameter ? "Parameter" : "Property/field")} {memberName} not found on type {type.FullName}");
    }
}

internal sealed class CsvTypeInfo<[DAM(Messages.ReflectionBound)] T>() : CsvTypeInfo(typeof(T))
{
    private static CsvTypeInfo<T>? _value;

    public static CsvTypeInfo Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value ?? GetOrInitInstance();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static CsvTypeInfo<T> GetOrInitInstance()
    {
        var instance = new CsvTypeInfo<T>();
        return Interlocked.CompareExchange(ref _value, instance, null) ?? instance;
    }
}
