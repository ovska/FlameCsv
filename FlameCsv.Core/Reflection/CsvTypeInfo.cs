using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using FlameCsv.Binding.Attributes;
using FlameCsv.Exceptions;

namespace FlameCsv.Reflection;

internal sealed class CsvTypeInfo<[DynamicallyAccessedMembers(Messages.ReflectionBound)] T>
{
    public static CsvTypeInfo<T> Instance => _instance ?? GetOrInitInstance();

    private static CsvTypeInfo<T>? _instance;

    public ReadOnlySpan<MemberData> Members => _members ?? GetOrInitPropertiesAndFields();
    public ReadOnlySpan<object> Attributes => _customAttributes ?? GetOrInitCustomAttributes();
    public ReadOnlySpan<ParameterData> ConstructorParameters
        => (_ctorParams ?? GetOrInitPrimaryCtorParameters()) is ParameterData[] pi
            ? pi
            : ThrowExceptionForNoPrimaryConstructor();

    private object[]? _customAttributes;
    private MemberData[]? _members;
    private object? _ctorParams;

    private CsvTypeInfo() { }

    public MemberData GetPropertyOrField(string memberName)
    {
        foreach (var member in Members)
        {
            if (member.Value.Name.Equals(memberName, StringComparison.Ordinal))
                return member;
        }

        return ThrowMemberNotFound(memberName);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private MemberData[] GetOrInitPropertiesAndFields()
    {
        var members = typeof(T)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Concat<MemberInfo>(typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public))
            .Select(static m => (MemberData)m)
            .ToArray();

        return Interlocked.CompareExchange(ref _members, members, null) ?? members;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private object[] GetOrInitCustomAttributes()
    {
        var attributes = typeof(T).GetCustomAttributes(inherit: true);
        return Interlocked.CompareExchange(ref _customAttributes, attributes, null) ?? attributes;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private object GetOrInitPrimaryCtorParameters()
    {
        var obj = Impl();
        Debug.Assert(obj is ParameterData[] || obj == Type.Missing);
        return Interlocked.CompareExchange(ref _ctorParams, obj, null) ?? _ctorParams;

        static object Impl()
        {
            var ctors = typeof(T).GetConstructors(BindingFlags.Instance | BindingFlags.Public);

            if (ctors.Length == 0)
            {
                return Array.Empty<ParameterData>();
            }
            else if (ctors.Length == 1)
            {
                return Array.ConvertAll(ctors[0].GetParameters(), p => (ParameterData)p);
            }

            ConstructorInfo? parameterlessCtor = null;

            foreach (var ctor in ctors)
            {
                var parameters = ctor.GetParameters();

                foreach (var attribute in ctor.GetCustomAttributes(inherit: false))
                {
                    if (attribute is CsvConstructorAttribute)
                        return Array.ConvertAll(parameters, p => (ParameterData)p);
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
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static CsvTypeInfo<T> GetOrInitInstance()
    {
        var instance = new CsvTypeInfo<T>();
        return Interlocked.CompareExchange(ref _instance, instance, null) ?? instance;
    }

    private static ParameterData[] ThrowExceptionForNoPrimaryConstructor()
    {
        throw new CsvBindingException(
            typeof(T), $"No empty constructor or constructor with [CsvConstructor] found for type {typeof(T)}");
    }

    private static MemberData ThrowMemberNotFound(string memberName)
    {
        throw new CsvConfigurationException($"Property/field {memberName} not found on type {typeof(T)}");
    }
}
