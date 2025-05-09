using System.Runtime.Serialization;
using FlameCsv.Attributes;
using FlameCsv.Reflection;

namespace FlameCsv.Tests.Binding;

// ReSharper disable UnusedParameter.Local
// ReSharper disable UnusedMember.Local
#pragma warning disable CS0414 // Field is assigned but its value is never used
#pragma warning disable CS9113 // Parameter is unread.

public static class TypeInfoTests
{
    [CsvConstructor(ParameterTypes = [typeof(int)])]
    private class OnType
    {
        public OnType(int _)
        {
        }

        public OnType(int i, bool b)
        {
        }
    }

    private class OnCtor
    {
        [CsvConstructor]
        public OnCtor(int _)
        {
        }

        public OnCtor()
        {
        }
    }

    private class OneCtor
    {
        public OneCtor(int _)
        {
        }
    }

    private class NoCtor;

    private class EmptyCtor
    {
        // ReSharper disable once EmptyConstructor
        public EmptyCtor()
        {
        }
    }

    [CsvConstructor(ParameterTypes = [typeof(int)])]
    private class Both
    {
        public Both(int _)
        {
        }

        [CsvConstructor]
        public Both(int i, bool b)
        {
        }
    }

    [Fact]
    public static void Should_Prioritize_Type_Ctor()
    {
        var info = new CsvTypeInfo(typeof(OnType));
        Assert.Equal(1, info.ConstructorParameters.Length);
        Assert.Equal(typeof(int), info.ConstructorParameters[0].Value.ParameterType);
    }

    [Theory]
    [InlineData(typeof(OnType))]
    [InlineData(typeof(OnCtor))]
    [InlineData(typeof(OneCtor))]
    public static void Should_Find_Ctor(Type type)
    {
        var info = new CsvTypeInfo(type);
        Assert.Equal(1, info.ConstructorParameters.Length);
        Assert.Equal(typeof(int), info.ConstructorParameters[0].Value.ParameterType);
    }

    [Theory]
    [InlineData(typeof(NoCtor))]
    [InlineData(typeof(EmptyCtor))]
    public static void Should_Find_EmptyCtor(Type type)
    {
        var info = new CsvTypeInfo(type);
        Assert.Equal(0, info.ConstructorParameters.Length);
    }

    [Fact]
    public static void Should_Cache_Members()
    {
        var m1 = CsvTypeInfo<Obj>.Value.Members.ToArray();
        var m2 = CsvTypeInfo<Obj>.Value.Members.ToArray();
        Assert.Equal(m1, m2, ReferenceEqualityComparer.Instance);

        Assert.Contains(
            m1,
            m => m.Value == typeof(Obj).GetProperty("Id") &&
                m.MemberType == typeof(int) &&
                m is { IsProperty: true, IsReadOnly: false, Attributes: [] });
        Assert.Contains(
            m1,
            m => m.Value == typeof(Obj).GetProperty("Name") &&
                m.MemberType == typeof(string) &&
                m is { IsProperty: true, IsReadOnly: true, Attributes: [] });
        Assert.Contains(
            m1,
            m => m.Value == typeof(Obj).GetField("Field") &&
                m is { IsProperty: false, IsReadOnly: false, Attributes: [CsvIgnoreAttribute] });

        foreach (var member in m1)
        {
            Assert.Same(member, CsvTypeInfo<Obj>.Value.GetPropertyOrField(member.Value.Name));
        }
    }

    [Fact]
    public static void Should_Cache_Attributes()
    {
        var a1 = CsvTypeInfo<Obj>.Value.Attributes.ToArray();
        var a2 = CsvTypeInfo<Obj>.Value.Attributes.ToArray();
        Assert.Equal(a1, a2, ReferenceEqualityComparer.Instance);

        // remove compiler generated attrs
        a1 = a1.Where(a => a.GetType().Namespace != "System.Runtime.CompilerServices").ToArray();
        Assert.Single(a1);
        Assert.IsType<DataContractAttribute>(a1[0]);
    }

    [Fact]
    public static void Should_Cache_Ctor_Params()
    {
        var p = CsvTypeInfo<Obj2>.Value.ConstructorParameters.ToArray();
        Assert.Equal(p, CsvTypeInfo<Obj2>.Value.ConstructorParameters.ToArray(), ReferenceEqualityComparer.Instance);

        Assert.Equal(2, p.Length);
        Assert.Equal(typeof(bool), p[0].Value.ParameterType);
        Assert.Equal(typeof(int), p[1].Value.ParameterType);
    }
}

[method: CsvConstructor]
file class Obj2(bool b, int i)
{
    public Obj2(string s) : this(s is null, 0)
    {
    }

    public bool B { get; set; } = b;
    public int I { get; set; } = i;
}

[DataContract]
file class Obj
{
    public Obj(int id)
    {
        Id = id;
    }

    [CsvConstructor]
    public Obj()
    {
    }

    public int Id { get; set; }
    public string? Name { get; }
    [CsvIgnore] public bool Field = true;
}
