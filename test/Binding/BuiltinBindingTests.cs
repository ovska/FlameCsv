using System.Runtime.CompilerServices;
using FlameCsv.Reflection;

namespace FlameCsv.Tests.Binding;

public static class BuiltinBindingTests
{
    private const string Data = "1,true,Alice\r\n2,false,Bob\r\n";

    [Fact]
    public static void Should_Bind_To_ValueTuple()
    {
        var items = Csv.From(Data).Read<(int i, bool b, string s)>(new CsvOptions<char> { HasHeader = false }).ToList();

        Assert.Equal(2, items.Count);
        Assert.Equal((1, true, "Alice"), items[0]);
        Assert.Equal((2, false, "Bob"), items[1]);
    }

    [Fact]
    public static void Should_Bind_To_Tuple()
    {
        var items = Csv.From(Data).Read<Tuple<int, bool, string>>(new CsvOptions<char> { HasHeader = false }).ToList();

        Assert.Equal(2, items.Count);
        Assert.Equal(new Tuple<int, bool, string>(1, true, "Alice"), items[0]);
        Assert.Equal(new Tuple<int, bool, string>(2, false, "Bob"), items[1]);
    }

    private class FakeTuple : ITuple
    {
        public int Length => throw new NotSupportedException();
        public object this[int index] => throw new NotSupportedException();
    }

    // ReSharper disable once UnusedTypeParameter
    private class FakeTuple<T> : FakeTuple;

    [Theory, MemberData(nameof(TupleTestData))]
    public static void Should_Check_If_Tuple(Type type, bool expected)
    {
        var actual = MaterializerExtensions.IsTuple(type);
        Assert.Equal(expected, actual);
    }

    public static TheoryData<Type, bool> TupleTestData() =>
        new()
        {
            { typeof(bool), false },
            { typeof(object), false },
            { typeof(ValueTuple), false },
            { typeof(ValueTuple<>), false },
            { typeof(ValueTuple<,>), false },
            { typeof((int a, int b)), true },
            { typeof(Tuple), false },
            { typeof(Tuple<>), false },
            { typeof(Tuple<,>), false },
            { typeof(Tuple<int, int>), true },
            // different module
            { typeof(FakeTuple), false },
            { typeof(FakeTuple<>), false },
            { typeof(FakeTuple<int>), false },
        };
}
