---
uid: attributes
---

# Attributes

Configuration attributes can be applied on a member, type, or assembly. Attributes on a type can be used to configure classes that you have no direct control over, such as on partial types in auto-generated code. Assembly scoped attributes can be used to configure types in other namespaces or assemblies that you cannot directly modify.

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

@"FlameCsv.Attributes.CsvHeaderAttribute" is used to configure the header name used when reading and writing CSV. The first parameter specifies the primary header name. Additional aliases can be provided as follow-up arguments to allow matching the member to alternative header values. The first parameter is always used when writing.

The default header value used it the member's name, e.g., `"IsAdmin"`. See also: @"FlameCsv.CsvOptions`1.IgnoreHeaderCase?displayProperty=nameWithType" and @"FlameCsv.CsvOptions`1.NormalizeHeader?displayProperty=nameWithType".

```cs
[CsvHeader("is_admin")]
public bool IsAdmin { get; set; }

[CsvHeader("id", "user_id")] // allow matching to user_id header
public int Id { get; set; }
```

## Constructor

@"FlameCsv.Attributes.CsvConstructorAttribute" is used to explicitly choose which constructor is used to instantiate a type. The selection process follows these rules:

1. @"FlameCsv.Attributes.CsvConstructorAttribute" is present, that constructor is used
2. No attribute is present, and a public parameterless constructor exists, it is used
3. If the type has exactly one public constructor, it is used

Priority for attributes is assembly > type > constructor.

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

[assembly: CsvConstructor(TargetType = typeof(User), ParameterTypes = [typeof(int), typeof(string)])]
```

## Required fields

To mark a property, field or parameter as required, use @"FlameCsv.Attributes.CsvRequiredAttribute".
If a required member has no matching field in the CSV header, an exception is thrown.

```cs
public class User
{
    public int Id { get; set; }

    [CsvRequired]
    public string Name { get; init; }
}
```

> [!NOTE]
> The following are implicitly treated as required:
> - Properties with `init` setter
> - Constructor parameters without default values

The `required`-keyword is not recognized by the library. Create an issue if you have a need for it.

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

Use @"FlameCsv.Attributes.CsvIgnoreAttribute" to exclude a member from CSV reading and writing operations.

```cs
public class User 
{
    public int Id { get; set; }
    
    [CsvIgnore]
    public DateTime LastModified { get; set; } // This field will be invisible to the library
}
```

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

## Overriding converters

Converters can be overridden on a per-member basis by using the @"FlameCsv.Attributes.CsvConverterAttribute".

```cs
class Transaction
{
    [CsvConverter<UnixTimestampConverter>]
    public DateTime Timestamp { get; set; }

    [CsvConverter<MoneyConverter>]
    public decimal Amount { get; set; }
}
```

The type must implement @"FlameCsv.CsvConverter`1" of the member type. The type can be either a converter or a factory.

## Reading interfaces or abstract classes

Use @"FlameCsv.Attributes.CsvTypeProxyAttribute" to specify a concrete type that should be instantiated when reading an interface or abstract class. The proxy type must be assignable to the target type (interface/abstract class).

```cs
[CsvTypeProxy(typeof(User))]
public interface IUser
{
    public int Id { get; }
    public string Name { get; }
}
```

> [!NOTE]
> This feature is still under development and its behavior may change in future releases.

