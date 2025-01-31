using System.Reflection;
using System.Runtime.CompilerServices;
using FlameCsv.Binding.Attributes;
using FlameCsv.Exceptions;
using FlameCsv.Utilities;
using DAMT = System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes;

namespace FlameCsv.Reflection;

internal static class CsvTypeInfo
{
    public readonly record struct CtorData(ConstructorInfo Value, ParameterData[] Params);

    private sealed class Cached<T>
    {
        public static Cached<T> Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _value ?? GetOrInitInstance();
        }

        private static Cached<T>? _value;

        internal object[]? _customAttributes;
        internal MemberData[]? _members;
        internal CtorData[]? _ctors;
        internal object? _ctorParams;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Cached<T> GetOrInitInstance()
        {
            var instance = new Cached<T>();
            return Interlocked.CompareExchange(ref _value, instance, null) ?? instance;
        }

        private Cached()
        {
            HotReloadService.RegisterForHotReload(
                this,
                static state =>
                {
                    var @this = (Cached<T>)state;
                    @this._customAttributes = null;
                    @this._members = null;
                    @this._ctors = null;
                    @this._ctorParams = null;
                });
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<MemberData> Members<[DAM(DAMT.PublicProperties | DAMT.PublicFields)] T>()
        => Cached<T>.Value._members ??= InitPropertiesAndFields<T>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<object> Attributes<T>()
        => Cached<T>.Value._customAttributes ??= InitCustomAttributes<T>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<CtorData> PublicConstructors<[DAM(DAMT.PublicConstructors)] T>()
        => Cached<T>.Value._ctors ??= InitConstructors<T>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<ParameterData> ConstructorParameters<[DAM(DAMT.PublicConstructors)] T>()
        => (Cached<T>.Value._ctorParams ??= InitPrimaryCtorParams<T>()) as ParameterData[] ??
            ThrowExceptionForNoPrimaryConstructor(typeof(T));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MemberData GetPropertyOrField<[DAM(DAMT.PublicProperties | DAMT.PublicFields)] T>(string memberName)
    {
        foreach (var member in Members<T>())
        {
            if (memberName == member.Value.Name)
            {
                return member;
            }
        }

        return (MemberData)ThrowMemberNotFound(typeof(T), memberName, isParameter: false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ParameterData GetParameter<[DAM(DAMT.PublicConstructors)] T>(string parameterName)
    {
        foreach (var parameter in ConstructorParameters<T>())
        {
            if (parameterName == parameter.Value.Name)
            {
                return parameter;
            }
        }

        return (ParameterData)ThrowMemberNotFound(typeof(T), parameterName, isParameter: false);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static MemberData[] InitPropertiesAndFields<[DAM(DAMT.PublicProperties | DAMT.PublicFields)] T>()
    {
        return typeof(T)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Concat<MemberInfo>(typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public))
            .Select(static m => (MemberData)m)
            .ToArray();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object[] InitCustomAttributes<T>() => typeof(T).GetCustomAttributes(inherit: true);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object InitPrimaryCtorParams<[DAM(DAMT.PublicConstructors)] T>()
    {
        var ctors = PublicConstructors<T>();

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
    private static CtorData[] InitConstructors<[DAM(DAMT.PublicConstructors)] T>()
        => typeof(T)
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
