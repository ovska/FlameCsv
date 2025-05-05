---
uid: converters
---

# Converters

## Overview

@"FlameCsv.CsvConverter`2" is used to convert types. It follows the `TryParse` / `TryFormat` -pattern.

The value passed to `TryParse` is the complete unescaped/unquoted CSV field, and should represent a complete value.
If the value is valid, the method should return `true` and the parsed value in the out parameter. If it is not valid,
the method must return `false`. This method must not throw exceptions even with invalid data, as the library
provides an informative exception if parsing fails.

The `TryFormat` method should attempt to write the specified value to the provided buffer, and return the number of characters written.
If the buffer is not large enough, the method must return `false`. If the value is invalid (e.g., it can never be written),
the method must throw an exception instead.

You can find converter examples in the [repository](https://github.com/ovska/FlameCsv/tree/main/FlameCsv.Core/Converters).

## Factories

The @"FlameCsv.CsvConverterFactory`1" type can be inherited to provide custom converters for types at runtime.
This pattern is also used in `System.Text.Json` to provide support for generic types and enums.

```cs
public class ListConverterFactory : CsvConverterFactory<char>
{
    public override bool CanConvert(Type type)
    {
        return type.IsAssignableTo(typeof(IList<>));
    }
    
    public override CsvConverter<char> CreateConverter(Type type, CsvOptions<char> options)
    {
        return (CsvConverter<char>)Activator.CreateInstance(
            typeof(MyListConverter<>).MakeGenericType(type),
            args: new object[] { options })!;
    }
}

CsvOptions<char> options = new()
{
    Converters = { new ListConverterFactory() }
};
```

> [!WARNING]
> Due to the dynamic code generation and reflection requirements of factories, they are not supported in NativeAOT applications.

## Custom

Custom converters can be added to @"FlameCsv.CsvOptions`1.Converters?displayProperty=nameWithType".
Converters are checked in "Last In, First Out" (LIFO) order, falling back to built-in converters if no user configured converter can convert a specific type.

```cs
CsvOptions<char> options = new()
{
    Converters = { new CustomIntConverter() }
};

// returns an instance of CustomIntConverter
CsvConverter<char, int> converter = options.GetConverter<int>();
```

## Built-in

### Configuration

The format provider can be configured on per-type basis with the @"FlameCsv.CsvOptions`1.FormatProviders?displayProperty=nameWithType" dictionary. If none is configured, the @"FlameCsv.CsvOptions`1.FormatProvider?displayProperty=nameWithType" property is used. This value defaults to @"System.Globalization.CultureInfo.InvariantCulture?displayProperty=nameWithType".

```cs
CsvOptions<char> options = new()
{
    FormatProvider = CultureInfo.CurrentCulture,
    FormatProviders = { [typeof(double)] = CultureInfo.InvariantCulture },
};

_ = options.GetFormatProvider(typeof(object)); // current
_ = options.GetFormatProvider(typeof(double)); // invariant
_ = options.GetFormatProvider(typeof(double?)); // invariant
```

> [!NOTE]
> All the type-indexed dictionaries consider value types and their nullable counterparts [equal](https://github.com/ovska/FlameCsv/blob/main/FlameCsv.Core/Utilities/NullableTypeEqualityComparer.cs), e.g., you only need to add either `int` or `int?` to the dictionary.

### Primitives

The following primitive types are supported by default:
- Numeric types (see below)
- @"System.String?text=string"
- @"System.Boolean?text=bool"
- @"System.DateTime"
- @"System.DateTimeOffset"
- @"System.TimeSpan"
- @"System.Guid"
- @"System.Char?text=char" (not considered a numeric type in FlameCSV)
- Any type implementing both @"System.ISpanParsable`1" and @"System.ISpanFormattable",
  or @"System.IUtf8SpanFormattable" and/r @"System.IUtf8SpanParsable`1" when converting to/from @"System.Byte?text=byte".

Most of these types can be configured using @"FlameCsv.CsvOptions`1.FormatProviders?displayProperty=nameWithType"
and @"FlameCsv.CsvOptions`1.Formats?displayProperty=nameWithType".

### Numeric types

Conversion of numeric types can be further configured with @"FlameCsv.CsvOptions`1.NumberStyles?displayProperty=nameWithType".
The default is @"System.Globalization.NumberStyles.Integer?displayProperty=nameWithType" for integer types,
and @"System.Globalization.NumberStyles.Float?displayProperty=nameWithType" for floating point types.

The following numeric types are supported by default:

- @"System.Int32?text=int"
- @"System.Double?text=double"
- @"System.Byte?text=byte"
- @"System.SByte?text=sbyte"
- @"System.Int16?text=short"
- @"System.UInt16?text=ushort"
- @"System.UInt32?text=uint"
- @"System.Int64?text=long"
- @"System.UInt64?text=ulong"
- @"System.Single?text=float"
- @"System.Decimal?text=decimal"

```cs
CsvOptions<char> options = new()
{
    NumberStyles = { [typeof(decimal)] = NumberStyles.Currency }
};

// decimals are explicitly formatted as currency
options.GetNumberStyles(typeof(decimal), defaultValue: NumberStyles.Float); // returns Currency
options.GetNumberStyles(typeof(double), defaultValue: NumberStyles.Float); // returns Float
```

### Enums

Enum format defaults to @"FlameCsv.CsvOptions`1.EnumFormat?displayProperty=nameWithType" unless configured explicitly
with @"FlameCsv.CsvOptions`1.Formats?displayProperty=nameWithType".

@"FlameCsv.CsvOptions`1.IgnoreEnumCase?displayProperty=nameWithType" can be used to make the parsing case-insensitive.

@"FlameCsv.CsvOptions`1.AllowUndefinedEnumValues?displayProperty=nameWithType" can be used to forego a `IsDefined`-check when parsing enums.

```cs
// more lenient enum parsing
CsvOptions<char> options = new()
{
    IgnoreEnumCase = true,
    AllowUndefinedEnumValues = true,
    EnumFormat = "G", // format as strings
};

var converter = options.GetConverter<DayOfWeek>();

converter.TryParse("SUNDAY", out _); // OK - ignore case
converter.TryParse("10", out _); // OK - undefined but valid number
converter.TryFormat(new char[64], DayOfWeek.Monday, out int charsWritten); // writes "Monday"  instead of "0"
```

> [!TIP]
> For extremely performant NativeAOT and trimming compatible enum converters, see the [source generator](source-generator.md#enum-converter-generator).

### Nullable types

@"System.Nullable`1" is supported by default as long as the underlying type can be converted.
When reading nullable types, @"FlameCsv.CsvOptions`1.NullTokens?displayProperty=nameWithType" can be used to specify
tokens that represent null values for each type. Otherwise, the converters default to
@"FlameCsv.CsvOptions`1.Null?displayProperty=nameWithType", which defaults to an empty string.

When writing any value that is null (both nullable structs or reference types), the configured null token is used.
Converters can signal to the writer that they have their own null handling with the @"FlameCsv.CsvConverter`2.CanFormatNull?displayProperty=nameWithType",
in which case a null `TValue` is passed to the converter's `TryFormat` method.

For performance reasons, the built-in converter for @"System.String" implements this behavior, and writes `null` as an empty string.
If you need to format empty and null strings differently, you can create a custom converter.

```cs
CsvOptions<char> options = new()
{
    Null = "null",
    NullTokens =
    {
        [typeof(int)] = "_",
        [typeof(string)] = "",
    }
};

options.GetNullToken(typeof(int?)); // returns "_"
options.GetNullToken(typeof(string)); // returns ""
options.GetNullToken(typeof(object)); // returns "null"
```

### Custom true/false values

Use @"FlameCsv.CsvOptions`1.BooleanValues?displayProperty=nameWithType" to customize which field values are parsed as booleans. You must specify at least one `true` and one `false` value.

Note that configuring custom boolean values globally _replaces_ the default parsing behavior. For more granular control, consider using @"FlameCsv.Attributes.CsvBooleanValuesAttribute" to configure values on per-member basis.

```cs
CsvOptions<char> options = new()
{
    BooleanValues =
    {
        ("1", true),
        ("0", false)
    }
};
```

> [!WARNING]
> When using global custom boolean values, @"FlameCsv.CsvOptions`1.Comparer?displayProperty=nameWithType" **must** be either @"System.StringComparer.Ordinal?displayProperty=nameWithType" or @"System.StringComparer.OrdinalIgnoreCase?displayProperty=nameWithType".
>
> The attribute supports case-sensitivity configuration on a per-member basis with
> @"FlameCsv.Attributes.CsvBooleanValuesAttribute.IgnoreCase?displayProperty=nameWithType".
