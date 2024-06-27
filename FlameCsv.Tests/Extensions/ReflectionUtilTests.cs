using System.Runtime.CompilerServices;
using FlameCsv.Reflection;

namespace FlameCsv.Tests.Extensions;

public static class ReflectionUtilTests
{
    private class FakeTuple : ITuple
    {
        public int Length => 0;
        public object? this[int index] => throw new NotImplementedException();
    }

    private class FakeTuple<T> : FakeTuple;

    [Theory, MemberData(nameof(TupleTestData))]
    public static void Should_Check_If_Tuple(Type type, bool expected)
    {
        var actual = ReflectionUtil.IsTuple(type);
        Assert.Equal(expected, actual);

        if (!type.IsGenericTypeDefinition)
        {
            var actual2 = ((Delegate)ReflectionUtil.IsTuple<object>)
                .Method
                .GetGenericMethodDefinition()
                .MakeGenericMethod(type)
                .Invoke(null, null);
            Assert.Equal(expected, actual2);
        }
    }

    public static IEnumerable<object[]> TupleTestData()
    {
        yield return new object[] { typeof(bool), false };
        yield return new object[] { typeof(object), false };
        yield return new object[] { typeof(ValueTuple), false };
        yield return new object[] { typeof(ValueTuple<>), false };
        yield return new object[] { typeof(ValueTuple<,>), false };
        yield return new object[] { typeof((int a, int b)), true };
        yield return new object[] { typeof(Tuple), false };
        yield return new object[] { typeof(Tuple<>), false };
        yield return new object[] { typeof(Tuple<,>), false };
        yield return new object[] { typeof(Tuple<int, int>), true };
        yield return new object[] { typeof(FakeTuple), false };
        yield return new object[] { typeof(FakeTuple<>), false };
        yield return new object[] { typeof(FakeTuple<int>), false };
    }
}
