using System.Reflection;
using System.Runtime.CompilerServices;
using FlameCsv.Binding.Attributes;
using FlameCsv.Exceptions;
using DAMT = System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes;

namespace FlameCsv.Reflection;

internal static class CsvTypeInfo
{
    private sealed class Cached<T>
    {
        public static Cached<T> Value => _value ?? GetOrInitInstance();
        private static Cached<T>? _value;

        internal object[]? _customAttributes;
        internal MemberData[]? _members;
        internal (ConstructorInfo Ctor, ParameterData[] Params)[]? _ctors;
        internal object? _ctorParams;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Cached<T> GetOrInitInstance()
        {
            var instance = new Cached<T>();
            return Interlocked.CompareExchange(ref _value, instance, null) ?? instance;
        }
    }

    public static ReadOnlySpan<MemberData> Members<[DAM(DAMT.PublicProperties | DAMT.PublicFields)] T>()
        => Cached<T>.Value._members ??= InitPropertiesAndFields<T>();

    public static ReadOnlySpan<object> Attributes<T>()
        => Cached<T>.Value._customAttributes ??= GetCustomAttributes<T>();

    public static ReadOnlySpan<(ConstructorInfo Ctor, ParameterData[] Params)>
        PublicConstructors<[DAM(DAMT.PublicConstructors)] T>()
        => Cached<T>.Value._ctors ??= GetOrInitConstructors<T>();

    public static ReadOnlySpan<ParameterData> ConstructorParameters<[DAM(DAMT.PublicConstructors)] T>()
        => (Cached<T>.Value._ctorParams ??= GetPrimaryCtorParams<T>()) as ParameterData[]
            ?? ThrowExceptionForNoPrimaryConstructor(typeof(T));

    public static ConstructorInfo? EmptyConstructor<[DAM(DAMT.PublicConstructors)] T>()
    {
        foreach (var (ctor, parameters) in PublicConstructors<T>())
        {
            if (parameters.Length == 0)
                return ctor;
        }

        return null;
    }

    public static MemberData GetPropertyOrField<[DAM(DAMT.PublicProperties | DAMT.PublicFields)] T>(string memberName)
    {
        foreach (var member in Members<T>())
        {
            if (member.Value.Name.Equals(memberName, StringComparison.Ordinal))
                return member;
        }

        return ThrowMemberNotFound(typeof(T), memberName);
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
    private static object[] GetCustomAttributes<T>() => typeof(T).GetCustomAttributes(inherit: true);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object GetPrimaryCtorParams<[DAM(DAMT.PublicConstructors)] T>()
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
                parameterlessCtor = ctor;
        }

        // No explicit ctor found, but found parameterless
        if (parameterlessCtor is not null)
        {
            return Array.Empty<ParameterData>();
        }

        return Type.Missing;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (ConstructorInfo, ParameterData[])[] GetOrInitConstructors<[DAM(DAMT.PublicConstructors)] T>()
        => typeof(T)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public)
            .Select(c => (c, c.GetParameters().Select(p => new ParameterData(p)).ToArray()))
            .ToArray();

    private static ParameterData[] ThrowExceptionForNoPrimaryConstructor(Type type)
    {
        throw new CsvBindingException(
            type,
            $"No empty constructor or constructor with [CsvConstructor] found for type {type.FullName}");
    }

    private static MemberData ThrowMemberNotFound(Type type, string memberName)
    {
        throw new CsvConfigurationException($"Property/field {memberName} not found on type {type.FullName}");
    }
}
