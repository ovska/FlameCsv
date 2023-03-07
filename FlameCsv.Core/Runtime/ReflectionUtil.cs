using System.Linq.Expressions;
using System.Reflection;
using CommunityToolkit.Diagnostics;
using FastExpressionCompiler;

namespace FlameCsv.Runtime;

internal static partial class ReflectionUtil
{
    private const string MustBeWritable = "Member must be an assignable property or field.";

    private const CompilerFlags DefaultCompilerFlags
            = CompilerFlags.ThrowOnNotSupportedExpression
#if DEBUG
            | CompilerFlags.EnableDelegateDebugInfo
#endif
        ;

    internal static Type MemberType(MemberInfo member)
    {
        return member switch
        {
            PropertyInfo { CanWrite: true } info => info.PropertyType,
            FieldInfo { IsInitOnly: false } info => info.FieldType,
            null => ThrowHelper.ThrowArgumentNullException<Type>(nameof(member)),
            _ => ThrowHelper.ThrowArgumentException<Type>(nameof(member), MustBeWritable),
        };
    }

    /// <summary>
    /// Validates that the member is a writable property or field.
    /// </summary>
    internal static MemberInfo ValidateMember(MemberInfo member)
    {
        return member switch
        {
            PropertyInfo { CanWrite: true } => member,
            FieldInfo { IsInitOnly: false } => member,
            null => ThrowHelper.ThrowArgumentNullException<MemberInfo>(nameof(member)),
            _ => ThrowHelper.ThrowArgumentException<MemberInfo>(nameof(member), MustBeWritable),
        };
    }

    public static MemberInfo GetMemberFromExpression(LambdaExpression memberExpression)
    {
        ArgumentNullException.ThrowIfNull(memberExpression);

        if (memberExpression.Body is MemberExpression { Member: var member })
            return member;

        // Func<T, object?> turns into implicit conversion e.g. x => (object)x.Id
        if (memberExpression.ReturnType == typeof(object)
            && memberExpression.Body is UnaryExpression
            {
                NodeType: ExpressionType.Convert,
                Operand: MemberExpression inner,
            })
        {
            return inner.Member;
        }

        return ThrowHelper.ThrowArgumentException<MemberInfo>(
            nameof(memberExpression),
            $"Parameter must be an expression targeting a property or field: {memberExpression}");
    }

    internal static MemberExpression GetMember(Expression target, MemberInfo member)
    {
        return member switch
        {
            PropertyInfo { CanWrite: true } prop => Expression.Property(target, prop),
            FieldInfo { IsInitOnly: false } fld => Expression.Field(target, fld),
            null => ThrowHelper.ThrowArgumentNullException<MemberExpression>(nameof(member)),
            _ => ThrowHelper.ThrowArgumentException<MemberExpression>(nameof(member), MustBeWritable),
        };
    }

    public static Action<T, TValue> CreateSetter<T, TValue>(Expression<Func<T, TValue>> propertyExpression)
    {
        if (propertyExpression.Body is MemberExpression { Member: var member })
            return CreateSetter<T, TValue>(member);

        return ThrowHelper.ThrowArgumentException<Action<T, TValue>>(
            nameof(propertyExpression),
            "Parameter must be a MemberExpression");
    }

    public static Action<T, TValue> CreateSetter<T, TValue>(MemberInfo member)
    {
        // (obj, value) => obj.Member = value
        var obj = Expression.Parameter(typeof(T), "obj");
        var value = Expression.Parameter(typeof(TValue), "value");
        var assignment = Expression.Assign(GetMember(obj, member), value);
        var lambda = Expression.Lambda<Action<T, TValue>>(assignment, obj, value);
        return lambda.TryCompileWithoutClosure<Action<T, TValue>>(flags: DefaultCompilerFlags)
            ?? lambda.CompileFast(flags: DefaultCompilerFlags);
    }
}
