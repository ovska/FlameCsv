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

    public static TheoryData<Type, bool> TupleTestData()
    {
        var data = new TheoryData<Type, bool>();
        data.Add(typeof(bool), false);
        data.Add(typeof(object), false);
        data.Add(typeof(ValueTuple), false);
        data.Add(typeof(ValueTuple<>), false);
        data.Add(typeof(ValueTuple<,>), false);
        data.Add(typeof((int a, int b)), true);
        data.Add(typeof(Tuple), false);
        data.Add(typeof(Tuple<>), false);
        data.Add(typeof(Tuple<,>), false);
        data.Add(typeof(Tuple<int, int>), true);
        data.Add(typeof(FakeTuple), false);
        data.Add(typeof(FakeTuple<>), false);
        data.Add(typeof(FakeTuple<int>), false);
        return data;
    }
}
