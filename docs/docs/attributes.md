---
uid: attributes
---

# Attributes

Configuration attributes can be applied on a member, type, or assembly. Attributes on a type can be used to configure classes that you have no direct control over, such as on partial typed in auto-generated code. Assembly scoped attributes can be used to configure types in other namespaces or assemblies that you cannot apply attributes on otherwise.

All three attributes in the example below do the same thing. For clarity, it is preferred to annotate the type directly whenever possible.

```cs
[assembly: CsvHeader("is_admin", TargetType = typeof(User), MemberName = nameof(User.IsAdmin))]

[CsvHeader("is_admin", MemberName = nameof(IsAdmin))]
class User
{
    [CsvHeader("is_admin")]
    public bool IsAdmin { get; set; }
}
```

## Header names

@"FlameCsv.Attributes.CsvHeaderAttribute" is used to configure the header name used when reading and writing CSV. This defaults. Additional aliases can be provided as follow up arguments to allow matching the member to header values. The first parameter is always used when writing.

```cs
[CsvHeader("is_admin")]
public bool IsAdmin { get; set; }

[CsvHeader("id", "user_id")] // allow matching to user_id header
public int Id { get; set; }
```

> [!NOTE]
> Case sensitivity is configured on per-options basis with @"FlameCsv.CsvOptions`1.Comparer?displayProperty=nameWithType", with the default being @"System.StringComparer.OrdinalIgnoreCase?displayProperty=nameWithType".

## Constructor

@"FlameCsv.Attributes.CsvConstructorAttribute" is used to explicitly choose a constructor that is used to instantiate a type. If omitted, a public parameterless constructor is used. Otherwise, if there is exactly one public constructor, it will be used. All other configurations result in an error.

```cs
public class User
{
    [CsvConstructor]
    public User(int id, string name)
    {
    }

    public User(int id)
    {
    }
}
```

If used on a type or assembly, specify the parameter types of the specific constructor:

```cs
[CsvConstructor(ParameterTypes = [typeof(int), typeof(string)])]
partial class User;

[assembly: CsvConstructor(TargetType = TargetType = typeof(User), ParameterTypes = [typeof(int), typeof(string)])]
```

## Required fields

To mark a property, field or parameter as required, use @"FlameCsv.Attributes.CsvRequiredAttribute".
If a required member has no match in a CSV header, an exception is thrown.

```cs
public class User
{
    public int Id { get; set; }

    [CsvRequired]
    public string Name { get; init; }
}
```

The `required`-keyword is not recognized by the library. Create an issue if you have a need for it.

> [!NOTE]
> `init`-only properties, and parameters without a default value are implicitly required.

## Field order

@"FlameCsv.Attributes.CsvOrderAttribute" can be used to explicitly set the order of fields in CSV. When omitted, 0 is used. Fields are sorted from smallest to largest, with equal values having no guarantees about their order.

```cs
public class User
{
    [CsvOrder(-1)] // ensure ID is always first
    public int Id { get; set; }

    [CsvOrder(1000)] // ensure name is always last
    public string Name { get; init; }

    // other members omitted
}
```

## Headerless CSV

@"FlameCsv.Attributes.CsvIndexAttribute" can be used to mark members for specific field indexes. These attributes **must** be set if @"FlameCsv.CsvOptions`1.HasHeader?displayProperty=nameWithType" is `false`.

```cs
public class User
{
    [CsvIndex(0)]
    public int Id { get; set; }

    [CsvIndex(1)]
    public string Name { get; set; }

    [CsvIndex(2)]
    public bool IsAdmin { get; set; }
}
```

## Ignoring members

TODO: figure out specific ignore semantics

## Ignoring indexes

When reading CSV without a header, you can choose to ignore one or more fields when reading by using @"FlameCsv.Attributes.CsvIgnoredIndexesAttribute" on the type.
Ignored fields are left empty when writing, and do nothing when reading.

```cs
[CsvIgnoredIndexes(1, 3)]
public class User
{
    [CsvIndex(0)]
    public int Id { get; set; }

    [CsvIndex(2)]
    public string Name { get; set; }
}
```

## Reading interfaces or abstract classes

To read interfaces or abstract classes (or types from other assemblies that cannot be directly constructed), use the "FlameCsv.Attributes.CsvTypeProxyAttribute" to configure a concrete type that is created in place of the actual type. The proxy type must be convertible to the interface or base type.

```cs
[CsvTypeProxy(typeof(User))]
public interface IUser
{
    public int Id { get; }
    public string Name { get; }
}
```

> [!NOTE]
> The specifics of this feature are still worked on, and might change in the future.
