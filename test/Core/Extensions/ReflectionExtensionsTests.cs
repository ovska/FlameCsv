using System.Diagnostics;
using FastExpressionCompiler.LightExpression;
using FlameCsv.Reflection;

namespace FlameCsv.Tests.Extensions;

public static class ReflectionExtensionsTests
{
    [Fact]
    public static void Should_Throw_For_Closure()
    {
        // ReSharper disable once ConvertToConstant.Local
        int variable = 0;
        System.Linq.Expressions.Expression<Func<int, int>> expression = x => x + variable;

        Assert.Throws<UnreachableException>(() => expression
            .ToLightExpression()
            .CompileLambda<Func<int, int>>(throwIfClosure: true));
    }

    [Fact]
    public static void Should_Throw_For_Invalid_Memberinfo()
    {
        var method = typeof(object).GetMethod("ToString")!;
        Assert.Throws<ArgumentException>(() => method.GetAsMemberExpression(Expression.FalseConstant));
    }

    [Fact]
    public static void Should_Throw_For_Invalid_MemberExpression()
    {
        System.Linq.Expressions.Expression<Func<string, int>> memExp = s => s.Length;
        Assert.Equal("Length", memExp.GetMemberInfo().Name);

        System.Linq.Expressions.Expression<Func<string, object>> unaryExp = s => s.Length;
        Assert.Equal("Length", memExp.GetMemberInfo().Name);

        System.Linq.Expressions.Expression<Func<string, object>> invalidExp = _ => 0;
        Assert.ThrowsAny<ArgumentException>(() => invalidExp.GetMemberInfo());
    }
}
