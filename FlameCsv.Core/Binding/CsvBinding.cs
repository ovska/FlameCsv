using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using CommunityToolkit.Diagnostics;
using FlameCsv.Binding.Attributes;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Runtime;

namespace FlameCsv.Binding;

// TODO: support for constructor parameters

/// <summary>
/// A structure binding a CSV column to a property or field of the deserialized type.
/// </summary>
public readonly struct CsvBinding : IEquatable<CsvBinding>
{
    /// <summary>
    /// Returns a binding that indicates the column at <paramref name="index"/> should be ignored.
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public static CsvBinding Ignore(int index) => new(index, IgnoreSingleton);

    /// <summary>
    /// Returns a binding to the property or field of the parameter expression.
    /// </summary>
    public static CsvBinding For<T>(int index, Expression<Func<T, object?>> memberExpression)
    {
        return new(index, ReflectionUtil.GetMemberFromExpression(memberExpression));
    }

    /// <summary>
    /// Member targeted by the binding.
    /// </summary>
    /// <remarks>
    /// If the column is ignored, represents a singleton ignored MemberInfo. See: <see cref="IsIgnored"/>
    /// </remarks>
    public MemberInfo Member { get; }

    /// <summary>
    /// Column index (zero based).
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Indicates if the binding's column should be skipped when parsing.
    /// </summary>
    public bool IsIgnored => Member.Equals(IgnoreSingleton);

    public Type Type => !IsIgnored
        ? ReflectionUtil.MemberType(Member)
        : ThrowHelper.ThrowInvalidOperationException<Type>("Cannot get type from ignored column");

    /// <summary>
    /// Initializes a binding between <paramref name="index"/> and <paramref name="member"/>.
    /// </summary>
    /// <param name="index">CSV column index</param>
    /// <param name="member">The member's PropertyInfo or FieldInfo</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if index is invalid</exception>
    /// <exception cref="ArgumentNullException">Throw if member is null</exception>
    /// <exception cref="ArgumentException">Throw if the member is not a writable property or field</exception>
    public CsvBinding(int index, MemberInfo member)
    {
        Guard.IsGreaterThanOrEqualTo(index, 0);
        Guard.IsNotNull(member);

        if (member.DeclaringType is { IsInterface: true })
            ThrowHelper.ThrowNotSupportedException("Interface binding is not yet supported.");

        Index = index;
        Member = member.Equals(IgnoreSingleton) ? member : ReflectionUtil.ValidateMember(member);
    }

    /// <inheritdoc cref="IsApplicableTo"/>
    public bool IsApplicableTo<TResult>() => IsApplicableTo(typeof(TResult));

    /// <summary>
    /// Checks if the binding can be used for the type.
    /// Returns true if either the column is ignored, or the member is defined in the type
    /// or a base class.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    public bool IsApplicableTo(Type targetType)
    {
        Guard.IsNotNull(targetType);

        if (IsIgnored)
            return true;

        var type = targetType;

        while (type is not null)
        {
            if (Member.DeclaringType == type)
                return true;

            type = type.BaseType;
        }

        return false;
    }

    internal ICsvParserOverride? GetParserOverride<TResult>()
    {
        ICsvParserOverride? found = null;

        foreach (var attribute in Member.GetCachedCustomAttributes())
        {
            if (attribute is ICsvParserOverride @override)
            {
                if (found is not null)
                    throw new CsvBindingException(typeof(TResult), this, found, @override);

                found = @override;
            }
        }

        return found;
    }

    internal MemberExpression AsExpression(Expression target) => ReflectionUtil.GetMember(target, Member);

    /// <summary>Returns the column index and member name.</summary>
    [ExcludeFromCodeCoverage]
    public override string ToString()
    {
        var sb = new StringBuilder(64);
        sb.Append("{ [");
        sb.Append(nameof(CsvBinding));
        sb.Append("] Index: ");
        sb.Append(Index);
        sb.Append(", ");

        if (IsIgnored)
        {
            sb.Append("Ignored");
        }
        else
        {
            sb.Append(Member is PropertyInfo ? "Property: " : "Field: ");
            sb.Append(Member.Name);
        }

        sb.Append(" }");
        return sb.ToString();
    }

    /// <summary><c>"Type.Property"</c></summary>
    internal string FormatMember => $"{Member.DeclaringType?.Name}.{Member.Name}".TrimStart('.');

    public bool Equals(CsvBinding other) => Index == other.Index && Member.Equals(other.Member);
    public override bool Equals(object? obj) => obj is CsvBinding other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Member, Index);
    public static bool operator ==(CsvBinding left, CsvBinding right) => left.Equals(right);
    public static bool operator !=(CsvBinding left, CsvBinding right) => !(left == right);

    /// <summary>Singleton MemberInfo used to indicate ignored columns.</summary>
    private static MemberInfo IgnoreSingleton => _ignore
        ??= typeof(CsvBinding).GetProperty(nameof(IgnoreSingleton), BindingFlags.Static | BindingFlags.NonPublic)!;

    private static MemberInfo? _ignore;
}
