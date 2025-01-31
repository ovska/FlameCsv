using System.Reflection;
using System.Runtime.Serialization;
using FlameCsv.Binding.Attributes;
using FlameCsv.Reflection;

namespace FlameCsv.Tests;

// ReSharper disable UnusedMember.Local
#pragma warning disable CS0414 // Field is assigned but its value is never used

public static class TypeCacheTests
{
    [Fact]
    public static void Should_Cache_Members()
    {
        var m1 = CsvTypeInfo.Members<Obj>().ToArray();
        var m2 = CsvTypeInfo.Members<Obj>().ToArray();
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
                m is { IsProperty: false, IsReadOnly: false, Attributes: [CsvFieldAttribute { IsIgnored: true }] });

        foreach (var member in m1)
        {
            Assert.Same(member, CsvTypeInfo.GetPropertyOrField<Obj>(member.Value.Name));
        }
    }

    [Fact]
    public static void Should_Cache_Attributes()
    {
        var a1 = CsvTypeInfo.Attributes<Obj>().ToArray();
        var a2 = CsvTypeInfo.Attributes<Obj>().ToArray();
        Assert.Equal(a1, a2, ReferenceEqualityComparer.Instance);

        // remove compiler generated attrs
        a1 = a1.Where(a => a.GetType().Namespace != "System.Runtime.CompilerServices").ToArray();
        Assert.Single(a1);
        Assert.IsType<DataContractAttribute>(a1[0]);
    }

    [Fact]
    public static void Should_Cache_Constructors()
    {
        var ctors = CsvTypeInfo.PublicConstructors<Obj>().ToArray();
        Assert.Equal(ctors, CsvTypeInfo.PublicConstructors<Obj>().ToArray());

        Assert.Equal(2, ctors.Length);

        Assert.Single(ctors, x => x.Params.Length == 0 && x.Value.GetCustomAttributes<CsvConstructorAttribute>().Any());
        Assert.Single(ctors, x => x.Params.Length == 1 && x.Params[0].Value.ParameterType == typeof(int));

        Assert.Empty(CsvTypeInfo.ConstructorParameters<Obj>().ToArray());
    }

    [Fact]
    public static void Should_Cache_Ctor_Params()
    {
        var p = CsvTypeInfo.ConstructorParameters<Obj2>().ToArray();
        Assert.Equal(p, CsvTypeInfo.ConstructorParameters<Obj2>().ToArray(), ReferenceEqualityComparer.Instance);

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
    [CsvField(IsIgnored = true)] public bool Field = true;
}
