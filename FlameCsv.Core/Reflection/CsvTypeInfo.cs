using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using FlameCsv.Attributes;
using FlameCsv.Exceptions;
using FlameCsv.Utilities;
using DAMT = System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes;

namespace FlameCsv.Reflection;

internal static class CsvTypeInfo<[DAM(Messages.ReflectionBound)] T>
{
    public static CsvTypeInfo Value { get; } = new(typeof(T));
}

internal class CsvTypeInfo
{
    private static readonly ParameterData[] _noCtorFoundSentinel = [null!];

    internal CsvTypeInfo([DAM(Messages.ReflectionBound)] Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        Type = type;

        HotReloadService.RegisterForHotReload(
            this,
            static state =>
            {
                var @this = (CsvTypeInfo)state;
                Interlocked.Exchange(ref @this._customAttributes, null);
                Interlocked.Exchange(ref @this._members, null);
                Interlocked.Exchange(ref @this._ctorParams, null);
                Interlocked.Exchange(ref @this._proxyOrSelf, null);
            });
    }

    [DAM(Messages.ReflectionBound)] public Type Type { get; }

    private object[]? _customAttributes;
    private MemberData[]? _members;
    private ParameterData[]? _ctorParams;
    private CsvTypeInfo? _proxyOrSelf;

    public ReadOnlySpan<MemberData> Members => _members ??= InitPropertiesAndFields(Type);

    public ReadOnlySpan<object> Attributes => _customAttributes ??= InitCustomAttributes(Type);

    public ReadOnlySpan<ParameterData> ConstructorParameters
    {
        get
        {
            if (ReferenceEquals(_ctorParams ??= InitPrimaryCtorParams(), _noCtorFoundSentinel))
            {
                return ThrowExceptionForNoPrimaryConstructor(Type);
            }

            return _ctorParams;
        }
    }

    public CsvTypeInfo ProxyOrSelf
    {
        get => _proxyOrSelf ??= TryGetTypeProxy(out var proxy) ? new CsvTypeInfo(proxy) : this;
    }

    public CsvTypeInfo? Proxy
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var value = ProxyOrSelf;
            return ReferenceEquals(value, this) ? null : value;
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
        if (Type.IsValueType ||
            GetFromAssemblyOrType<CsvTypeProxyAttribute>() is not { } attribute)
        {
            typeProxy = null;
            return false;
        }

        typeProxy = attribute.CreatedTypeProxy;

        if (typeProxy.IsInterface || typeProxy.IsAbstract)
        {
            throw new CsvBindingException(
                Type,
                $"Invalid type proxy for {Type}: Type {typeProxy.FullName} cannot be instantiated.");
        }

        if (!typeProxy.IsAssignableTo(Type))
        {
            throw new CsvBindingException(
                Type,
                $"Invalid type proxy for {Type}: Not assignable to {typeProxy.FullName}).");
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private ParameterData[] InitPrimaryCtorParams()
    {
        CsvConstructorAttribute? ctorAttr = GetFromAssemblyOrType<CsvConstructorAttribute>();

        if (ctorAttr is not null)
        {
            if (ctorAttr.ParameterTypes is null)
            {
                throw new CsvConfigurationException(
                    $"Parameter types not set for [CsvConstructor] on type {Type.FullName}");
            }

            var ctor = Type.GetConstructor(BindingFlags.Instance | BindingFlags.Public, types: ctorAttr.ParameterTypes);

            if (ctor is null)
            {
                throw new CsvBindingException(
                    Type,
                    $"Constructor with parameter types {string.Join(", ", ctorAttr.ParameterTypes.Select(t => t.FullName))} not found.");
            }

            return GetResult(ctor.GetParameters());
        }

        var ctors = Type.GetConstructors(BindingFlags.Instance | BindingFlags.Public);

        if (ctors.Length == 0) return [];
        if (ctors.Length == 1) return GetResult(ctors[0].GetParameters());

        ParameterInfo[]? bestMatch = null;

        foreach (var ctor in ctors)
        {
            var parameters = ctor.GetParameters();

            if (parameters.Length == 0)
            {
                bestMatch ??= [];
                continue;
            }

            foreach (var attribute in ctor.GetCustomAttributes(inherit: false))
            {
                if (attribute is CsvConstructorAttribute)
                {
                    bestMatch = parameters;
                    break;
                }
            }

            if (bestMatch == parameters) break;
        }

        if (bestMatch is null)
        {
            return _noCtorFoundSentinel;
        }

        return GetResult(bestMatch);

        static ParameterData[] GetResult(ParameterInfo[] parameters)
            => parameters.Select(static p => (ParameterData)p).ToArray();
    }

    private TAttribute? GetFromAssemblyOrType<TAttribute>() where TAttribute : Attribute
    {
        foreach (var attribute in AssemblyAttributes.Get(Type))
        {
            if (attribute is TAttribute result) return result;
        }

        foreach (var attribute in Attributes)
        {
            if (attribute is TAttribute result) return result;
        }

        return null;
    }

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
