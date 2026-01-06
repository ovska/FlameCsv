---
uid: configuration
---

# Configuration

## Overview

Aside from [attributes](attributes.md), configuration is mainly done through the @"FlameCsv.CsvOptions`1" class. Similar to `System.Text.Json`, options instances should be configured once and reused for the application lifetime. Creating a new options instance for every operation is not catastrophic, but will have a slight performance impact.

After an options instance is used to read or write CSV, it cannot be modified (see @"FlameCsv.CsvOptions`1.IsReadOnly"). The options instances are thread-safe to *use*, but not to *configure*. You can call @"FlameCsv.CsvOptions`1.MakeReadOnly?displayProperty=nameWithType" to ensure thread safety by making the options instance immutable.

For convenience, a copy-constructor @"FlameCsv.CsvOptions`1.%23ctor(FlameCsv.CsvOptions{`0})" is available, for example if you need slightly different configuration for reading and writing. This copies over all the configurable properties.

## Default Options

The static @"FlameCsv.CsvOptions`1.Default?displayProperty=nameWithType" property provides access to default configuration. This is used when `null` options are passed to @"FlameCsv.Csv". The default options are read-only and have identical configuration to a new instance created with `new()`.

Default options are only available for @"System.Char?text=char" (UTF-16) and @"System.Byte?text=byte" (UTF-8).

## Dialect

### Delimiter
The field separator is configured with @"FlameCsv.CsvOptions`1.Delimiter?displayProperty=nameWithType". The default value is `,` (comma). Other common values include `\t` and `;`.

### Quote
The string delimiter is configured with @"FlameCsv.CsvOptions`1.Quote?displayProperty=nameWithType". The default value is`"` (double-quote). CSV fields wrapped in quotes (also referred to as strings) can contain otherwise special characters such as delimiters. A quote inside a string is escaped with another quote, e.g. `"James ""007"" Bond"`. If you know your data does not contain quoted fields, you can improve performance by setting the value to`null`(this also requires that @"FlameCsv.CsvOptions`1.FieldQuoting?displayProperty=nameWithType" is disabled).

### Newline
The newline type (record separator) is configured with @"FlameCsv.CsvOptions`1.Newline?displayProperty=nameWithType". The default value is `\r\n`, which will also accept lone `\n` or `\r` when reading. The configured value is used as-is while writing. You can also configure it to be platform-specific. If you know your data only contains `\n`, you can improve performance by configuring the dialect accordingly. Otherwise, the safe choice is to leave it as the default.

### Trimming
The @"FlameCsv.CsvOptions`1.Trimming?displayProperty=nameWithType" property is used to configure whether spaces are trimmed from fields when reading. The default value is @"FlameCsv.CsvFieldTrimming.None?displayProperty=nameWithType". The flags-enum supports trimming leading and trailing spaces, or both. For compliance with other CSV libraries, whitespace characters other than ASCII-space (0x20) are not trimmed.

This property is only used when reading CSV, and has no effect when writing, see @"FlameCsv.CsvOptions`1.FieldQuoting?displayProperty=nameWithType" for writing.

> [!WARNING]
> For performance reasons, all the dialect characters must be non-alphanumeric ASCII (numeric value 1..127 inclusive).

### Example

```cs
CsvOptions<byte> options = new()
{
    Delimiter = '\t',
    Quote = '"',
    Newline = CsvNewline.LF,
    Trimming = CsvFieldTrimming.Both,
    FieldQuoting = CsvFieldQuoting.Auto | CsvFieldQuoting.LeadingOrTrailingSpaces,
};
```

## Header

The @"FlameCsv.CsvOptions`1.HasHeader?displayProperty=nameWithType" property is`true`by default, which expects a header record on the first line/record. The comparison is case-insensitive by default (configured via @"FlameCsv.CsvOptions`1.IgnoreHeaderCase"). A delegate @"FlameCsv.CsvOptions`1.NormalizeHeader" can be used to process header names before they are compared (note that this disables the header string value pooling).

```cs
const string csv = "id,name\n1,Bob\n2,Alice\n";

List<User> users = Csv.From(csv).Read<User>(new CsvOptions<char> { HasHeader = true });
```

## Parsing and formatting fields

See @"converters" for an overview on converter configuration, implementation, and what converters are supported by default.

## Quoting fields when writing

The @"FlameCsv.CsvFieldQuoting" enumeration and @"FlameCsv.CsvOptions`1.FieldQuoting?displayProperty=nameWithType" property are used to configure the behavior when writing CSV. The default, @"FlameCsv.CsvFieldQuoting.Auto?displayProperty=nameWithType" only quotes fields if they contain special characters or whitespace.

```cs
// quote all fields, e.g., for noncompliant 3rd party libraries
return new CsvOptions<char>() { FieldQuoting = CsvFieldQuoting.Always };
```

If you are 100% sure your data does not contain any special characters, you can set it to @"FlameCsv.CsvFieldQuoting.Never?displayProperty=nameWithType" to squeeze out a little bit of performance by omitting the check if each written field needs to be quoted. If @"FlameCsv.CsvOptions`1.Quote" is set to `null`, an exception is thrown if this setting is not @"FlameCsv.CsvFieldQuoting.Never?displayProperty=nameWithType".


## Skipping records or resetting headers

The @"FlameCsv.CsvOptions`1.RecordCallback?displayProperty=nameWithType" property is used to configure a custom callback.
The argument contains metadata about the current record, and can be used to skip records or reset the header record.
This can be used to read multiple different "documents" out of the same data stream.

Below is an example of a callback that resets the headers and bindings on empty lines, and skips records that start with `#`.

```cs
CsvOptions<char> options = new()
{
    RecordCallback = (ref readonly CsvRecordCallbackArgs<char> args) =>
    {
        if (args.IsEmpty)
        {
            // reset the current headers and bindings on empty lines
            args.HeaderRead = false;
        }
        else if (args.Record[0] == '#')
        {
            // skip records that start with #
            args.SkipRecord = true;
        }
    }
};
```

> [!WARNING]
> Comment lines are not supported! Even if you configure the callback to skip rows that start with `#`, the rows are still parsed and expected to be properly structured CSV (e.g., no unbalanced quotes).

## Advanced topics

### NativeAOT

Since any implementation of @"FlameCsv.CsvConverterFactory`1" (including built-in nullable and enum factories) can potentially require unreferenced types or dynamic code, the default @"FlameCsv.CsvOptions`1.GetConverter``1?displayProperty=nameWithType" method is not AOT-compatible.

Use @"FlameCsv.CsvOptions`1.Aot?displayProperty=nameWithType" to retrieve a wrapper around the configured converters, which provides convenience methods to safely retrieve converters for types known at runtime. See the documentation on methods of @"FlameCsv.CsvOptions`1.AotSafeConverters" for more info. This property is used by the source generator.

```cs
// aot-safe default nullable and enum converters if not configured by user
CsvConverter<char, int?> c1 = options.Aot.GetOrCreateNullable(static o => o.Aot.GetConverter<int>());
CsvConverter<char, DayOfWeek> c2 = options.Aot.GetOrCreateEnum<DayOfWeek>();
```

### Buffer sizes and memory pooling

You can configure the I/O options with @"FlameCsv.IO.CsvIOOptions" when using streamed reading/writing, including buffer sizes (defaulting to 16 kB, and 32 kB for file I/O) and whether to close the Stream/TextWriter passed.

You can configure how memory is allocated by assigning a custom @"FlameCsv.IO.IBufferPool". This memory is used to handle escaping, unescaping, and buffers to read data from streaming sources. The default value uses the shared array pool.

The library also maintains a small pool of @"System.String"-instances of previously encountered headers, so unless your data is exceptionally varied, the allocation cost is paid only once.

### Custom binding

If you don't want to use the built-in @"FlameCsv.Binding.CsvReflectionBinder`1" (attribute configuration), set @"FlameCsv.CsvOptions`1.TypeBinder?displayProperty=nameWithType" property to your custom implementation implementing @"FlameCsv.Binding.ICsvTypeBinder`1".
