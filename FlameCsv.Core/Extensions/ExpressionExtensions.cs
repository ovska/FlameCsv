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

    /// <summary>
    /// Compiles a lambda expression into a delegate using FastExpressionCompiler.
    /// </summary>
    public static TDelegate CompileLambda<TDelegate>(this LambdaExpression lambda)
        where TDelegate : class
    {
        return lambda.TryCompileWithoutClosure<TDelegate>(flags: DefaultCompilerFlags)
            ?? lambda.CompileFast<TDelegate>(flags: DefaultCompilerFlags);
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
