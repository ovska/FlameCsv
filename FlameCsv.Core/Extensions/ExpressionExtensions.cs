using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using FastExpressionCompiler.LightExpression;

namespace FlameCsv.Extensions;

internal static class ExpressionExtensions
{
    private const CompilerFlags DefaultCompilerFlags
        = CompilerFlags.ThrowOnNotSupportedExpression
#if DEBUG
        | CompilerFlags.EnableDelegateDebugInfo
#endif
    ;

    [DoesNotReturn]
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
    [RUF(Messages.Reflection)]
    [RDC(Messages.DynamicCode)]
    public static TDelegate CompileLambda<TDelegate>(this LambdaExpression lambda, bool throwIfClosure)
        where TDelegate : Delegate
    {
        TDelegate? fn = lambda.TryCompileWithoutClosure<TDelegate>(flags: DefaultCompilerFlags);

        if (fn is null && throwIfClosure)
            ThrowForClosure(lambda);

        return fn ?? lambda.CompileFast<TDelegate>(flags: DefaultCompilerFlags);
    }

    public static (MemberExpression, Type) GetAsMemberExpression(this MemberInfo memberInfo, Expression target)
    {
        return memberInfo switch
        {
            PropertyInfo { CanRead: true } property => (Expression.Property(target, property), property.PropertyType),
            FieldInfo field => (Expression.Field(target, field), field.FieldType),
            _ => throw new ArgumentException(
                $"Parameter must be a readable property or field: {memberInfo}",
                paramName: nameof(memberInfo)),
        };
    }

    public static MemberInfo GetMemberInfo(this System.Linq.Expressions.LambdaExpression memberExpression)
    {
        ArgumentNullException.ThrowIfNull(memberExpression);

        if (memberExpression.Body is System.Linq.Expressions.MemberExpression { Member: var member })
            return member;

        // Func<T, object?> turns into implicit conversion e.g. x => (object)x.Id
        if (memberExpression.ReturnType == typeof(object)
            && memberExpression.Body is System.Linq.Expressions.UnaryExpression
            {
                NodeType: System.Linq.Expressions.ExpressionType.Convert,
                Operand: System.Linq.Expressions.MemberExpression inner,
            })
        {
            return inner.Member;
        }

        throw new ArgumentException(
            $"Parameter must be an expression targeting a property or field: {memberExpression}",
            nameof(memberExpression));
    }
}
