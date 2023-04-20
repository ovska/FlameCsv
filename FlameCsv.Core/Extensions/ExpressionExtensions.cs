using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using CommunityToolkit.Diagnostics;
using FastExpressionCompiler;

namespace FlameCsv.Extensions;

internal static class ExpressionExtensions
{
    private const CompilerFlags DefaultCompilerFlags
        = CompilerFlags.ThrowOnNotSupportedExpression
#if DEBUG
        | CompilerFlags.EnableDelegateDebugInfo
#endif
    ;

    [StackTraceHidden, DoesNotReturn]
    private static void ThrowForClosure(Expression expression)
    {
        string asString;
        Exception? inner = null;

        try
        {
            asString = expression.ToString();
        }
        catch (Exception e)
        {
            asString = "<failed to get string>";
            inner = e;
        }

        throw new UnreachableException($"Expected lambda to have no closure, but compiling it failed: {asString}", inner);
    }

    /// <summary>
    /// Compiles a lambda expression into a delegate using FastExpressionCompiler.
    /// </summary>
    public static TDelegate CompileLambda<TDelegate>(this LambdaExpression lambda, bool throwIfClosure = false)
        where TDelegate : Delegate
    {
        TDelegate? fn = lambda.TryCompileWithoutClosure<TDelegate>(flags: DefaultCompilerFlags);

        if (fn is null && throwIfClosure)
            ThrowForClosure(lambda);

        return fn ?? lambda.CompileFast<TDelegate>(flags: DefaultCompilerFlags);
    }

    /// <summary>
    /// Compiles a lambda expression with a closure (not static) into a delegate using FastExpressionCompiler.
    /// </summary>
    public static TDelegate CompileLambdaWithClosure<TDelegate>(this LambdaExpression lambda)
        where TDelegate : Delegate
    {
        Debug.Assert(lambda.TryCompileWithoutClosure<TDelegate>(flags: DefaultCompilerFlags) is null);
        return lambda.CompileFast<TDelegate>(flags: DefaultCompilerFlags);
    }

    public static (MemberExpression, Type) GetAsMemberExpression(this MemberInfo memberInfo, Expression target)
    {
        return memberInfo switch
        {
            PropertyInfo { CanRead: true } property => (Expression.Property(target, property), property.PropertyType),
            FieldInfo field => (Expression.Field(target, field), field.FieldType),
            _ => ThrowHelper.ThrowArgumentException<(MemberExpression, Type)>(
                nameof(memberInfo),
                $"Parameter must be a readable property or field: {memberInfo}"),
        };
    }

    public static MemberInfo GetMemberInfo(this LambdaExpression memberExpression)
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
}
