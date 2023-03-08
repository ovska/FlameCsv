using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.Diagnostics;
using FlameCsv.Binding.Attributes;
using FlameCsv.Extensions;
using FlameCsv.Runtime;

namespace FlameCsv.Binding;

public readonly struct CsvBinding : IEquatable<CsvBinding>, IComparable<CsvBinding>
{
    internal const AttributeTargets AllowedOn
        = AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter;

    private enum CsvBindingType : byte
    {
        Ignored = 1,
        ConstructorParameter = 2,
        Property = 3,
        Field = 4,
    }

    /// <summary>Marks the column at <paramref name="index"/> as ignored.</summary>
    public static CsvBinding Ignore(int index) => new(index);

    public static CsvBinding For<T>(int index, Expression<Func<T, object?>> memberExpression)
    {
        return ForMember(index, ReflectionUtil.GetMemberFromExpression(memberExpression));
    }

    public static CsvBinding ForMember(int index, MemberInfo member)
    {
        return member is PropertyInfo pi ? new(index, pi) : new(index, (FieldInfo)member);
    }

    internal static CsvBinding FromHeaderBinding(in HeaderBindingArgs candidate)
    {
        return candidate.Target switch
        {
            PropertyInfo p => new(candidate.Index, p),
            FieldInfo f => new(candidate.Index, f),
            ParameterInfo p => new(candidate.Index, p),
            _ => ThrowHelper.ThrowInvalidOperationException<CsvBinding>("Invalid HeaderBindingArgs"),
        };
    }

    /// <summary>
    /// The CSV column index of this binding.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Returns the underlying member info, <see cref="IsMember"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException"/>
    public MemberInfo Member => IsMember
        ? (MemberInfo)_object
        : ThrowHelper.ThrowInvalidOperationException<MemberInfo>("Binding is not a member.");

    /// <summary>
    /// Returns the underlying parameter info, <see cref="IsProperty"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException"/>
    public PropertyInfo Property => IsProperty
        ? (PropertyInfo)_object
        : ThrowHelper.ThrowInvalidOperationException<PropertyInfo>("Binding is not a property.");

    /// <summary>
    /// Returns the underlying parameter info, <see cref="IsField"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException"/>
    public FieldInfo Field => IsField
        ? (FieldInfo)_object
        : ThrowHelper.ThrowInvalidOperationException<FieldInfo>("Binding is not a property.");

    /// <summary>
    /// Returns the underlying parameter info, <see cref="IsParameter"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException"/>
    public ParameterInfo Parameter => IsParameter
        ? (ParameterInfo)_object
        : ThrowHelper.ThrowInvalidOperationException<ParameterInfo>("Binding is a member.");

    /// <summary>
    /// Returns the type of the binding's target (property/field/parameter type).
    /// For ignored fields, returns <c>typeof(object)</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException"/>
    public Type Type => _type switch
    {
        CsvBindingType.Property => ((PropertyInfo)_object).PropertyType,
        CsvBindingType.Field => ((FieldInfo)_object).FieldType,
        CsvBindingType.ConstructorParameter => ((ParameterInfo)_object).ParameterType,
        CsvBindingType.Ignored => typeof(object),
        // can only get here with default struct
        _ => ThrowHelper.ThrowInvalidOperationException<Type>($"The {nameof(CsvBinding)} struct is uninitialized"),
    };

    /// <summary>
    /// Returns the constructor of the parameter, see <see cref="IsParameter"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException"/>
    public ConstructorInfo Constructor => (ConstructorInfo)Parameter.Member;

    /// <summary>
    /// Returns the type of the binding's target.
    /// </summary>
    /// <exception cref="InvalidOperationException"/>
    public Type? DeclaringType => _object switch
    {
        MemberInfo member => member.DeclaringType,
        ParameterInfo { Member: var member } => member.DeclaringType,
        _ => ThrowHelper.ThrowInvalidOperationException<Type?>("Cannot get declaring type from binding"),
    };

    /// <summary>Returns whether the binding is on an ignored column.</summary>
    public bool IsIgnored => _type is CsvBindingType.Ignored;

    /// <summary>Returns whether the binding targets a property or a field.</summary>
    public bool IsMember => _type is CsvBindingType.Property or CsvBindingType.Field;

    /// <summary>Returns whether the binding targets a property.</summary>
    public bool IsProperty => _type is CsvBindingType.Property;

    /// <summary>Returns whether the binding targets a field.</summary>
    public bool IsField => _type is CsvBindingType.Field;

    /// <summary>Returns whether the binding targets a constructor parameter.</summary>
    public bool IsParameter => _type is CsvBindingType.ConstructorParameter;

    private readonly object _object;
    private readonly CsvBindingType _type;

    public CsvBinding(int index, PropertyInfo property) : this((object)property, index)
    {
        ArgumentNullException.ThrowIfNull(property);
        GuardEx.IsNotInterfaceDefined(property);
        _type = CsvBindingType.Property;
    }

    public CsvBinding(int index, FieldInfo @field) : this((object)@field, index)
    {
        ArgumentNullException.ThrowIfNull(@field);
        GuardEx.IsNotInterfaceDefined(@field);
        _type = CsvBindingType.Field;
    }

    public CsvBinding(int index, ParameterInfo parameter) : this((object)parameter, index)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        Guard.IsAssignableToType<ConstructorInfo>(parameter.Member);
        _type = CsvBindingType.ConstructorParameter;
    }

    /// <summary>Ctor for ignored column</summary>
    private CsvBinding(int index) : this(Type.Missing, index)
    {
        _type = CsvBindingType.Ignored;
    }

    private CsvBinding(object @object, int index)
    {
        Guard.IsGreaterThanOrEqualTo(index, 0);
        Index = index;
        _object = @object;
    }

    /// <inheritdoc cref="IsApplicableTo"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsApplicableTo<TResult>() => IsApplicableTo(typeof(TResult));

    /// <summary>
    /// Checks if the binding can be used for the type.
    /// Returns true if either the column is ignored, or the member is defined in the type
    /// or a base class.
    /// </summary>
    public bool IsApplicableTo(Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        if (IsIgnored)
            return true;

        // TODO: binding on interface property with a setter?
        Type? type = targetType;
        Type? declaringType = DeclaringType;

        while (type is not null)
        {
            if (declaringType == type)
                return true;

            type = type.BaseType;
        }

        return false;
    }

    internal bool TryGetParserOverride([NotNullWhen(true)] out CsvParserOverrideAttribute? @override)
    {
        @override = null;

        if (IsIgnored)
            return false;

        object[] attributes = IsMember
            ? Member.GetCachedCustomAttributes()
            : Parameter.GetCachedParameterAttributes();

        foreach (var attribute in attributes)
        {
            if (attribute is CsvParserOverrideAttribute match)
            {
                @override = match;
                return true;
            }
        }

        @override = null;
        return false;
    }

    /// <inheritdoc/>
    public bool Equals(CsvBinding other)
        => Index == other.Index
        && _type == other._type
        && _object == other._object;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CsvBinding csvBinding && Equals(csvBinding);
    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Index, _object);
    /// <inheritdoc/>
    public int CompareTo(CsvBinding other) => Index.CompareTo(other.Index);

    /// <inheritdoc/>
    public static bool operator ==(CsvBinding left, CsvBinding right) => left.Equals(right);
    /// <inheritdoc/>
    public static bool operator !=(CsvBinding left, CsvBinding right) => !(left == right);

    /// <summary>
    /// Returns whether the binding targets the same property/field/parameter,
    /// or both bindings are ignored columns.
    /// </summary>
    public bool TargetEquals(CsvBinding other) => _object == other._object;

    /// <summary>Returns the column index and member name.</summary>
    [ExcludeFromCodeCoverage]
    public override string ToString()
    {
        var sb = new StringBuilder(64);
        sb.Append($"{{ [{nameof(CsvBinding)}] Index: {Index}, ");

        if (IsIgnored)
        {
            sb.Append("Ignored");
        }
        else if (IsParameter)
        {
            sb.Append("Parameter: ");
            sb.Append(Type.Name);
            sb.Append(' ');
            sb.Append(Parameter.Name);
        }
        else
        {
            sb.Append(IsProperty ? "Property: " : "Field: ");
            sb.Append(Type.Name);
            sb.Append(' ');
            sb.Append(Member.Name);
        }

        sb.Append(" }");
        return sb.ToString();
    }
}
