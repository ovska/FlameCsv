---
uid: configuration
---

# Configuration

## Overview

Aside from [attributes](attributes.md), configuration is mainly done through the @"FlameCsv.CsvOptions`1" class. Similar to `System.Text.Json`, options instances should be configured once and reused for the application lifetime. After an options instance is used to read or write CSV, it cannot be modified (see @"FlameCsv.CsvOptions`1.IsReadOnly"). The options instances are thread-safe to *use*, but not to *configure*. You can call @"FlameCsv.CsvOptions`1.MakeReadOnly?displayProperty=nameWithType" to ensure thread safety by making the options instance immutable.

For convenience, a copy-constructor @"FlameCsv.CsvOptions`1.%23ctor(FlameCsv.CsvOptions{`0})" is available, for example if you need slightly different configuration for reading and writing. This copies over all the configurable properties.

## Default Options

The static @"FlameCsv.CsvOptions`1.Default?displayProperty=nameWithType" property provides access to default configuration. This is used when `null` options are passed to @"FlameCsv.CsvReader" or @"FlameCsv.CsvWriter". The default options are read-only and have identical configuration to a new instance created with `new()`.

Default options are only available for @"System.Char?text=char" (UTF-16) and @"System.Byte?text=byte" (UTF-8).

```cs
CsvConverter<byte, int> intConverter = CsvOptions<byte>.Default.GetConverter<int>();
```

## Dialect

### Delimiter
The field separator is configured with @"FlameCsv.CsvOptions`1.Delimiter?displayProperty=nameWithType". The default value is `,` (comma). Other common values include `\t` and `;`.

### Quote
The string delimiter is configured with @"FlameCsv.CsvOptions`1.Quote?displayProperty=nameWithType". The default value is `"` (double-quote). CSV fields wrapped in quotes (also referred to as strings) can contain otherwise special characters such as delimiters. A quote inside a string is escaped with another quote, e.g. `"James ""007"" Bond"`.

### Newline
The record separator is configured with @"FlameCsv.CsvOptions`1.Newline?displayProperty=nameWithType". The default value is `\r\n`. FlameCSV is lenient when parsing newlines, and a `\r\n`-configured reader can read only `\n` or `\r`. The value is used as-is when writing. If you know the data is always in a specific format, you can set the value to `\n` or `\r` to squeeze out an extra 1-2% of performance. You can use any custom newline as well, as long as it is 1 or 2 characters long, and does not contain two of the same character (such as `\r\r` or `\n\n`).

### Trimming
The @"FlameCsv.CsvOptions`1.Trimming?displayProperty=nameWithType" property is used to configure whether spaces are trimmed from fields when reading. The default value is @"FlameCsv.Writing.CsvFieldTrimming.None?displayProperty=nameWithType". The flags-enum supports trimming leading and trailing spaces, or both.
This property is only used when reading CSV, and has no effect when writing, see @"FlameCsv.CsvOptions`1.FieldQuoting?displayProperty=nameWithType" for writing.

### Escape
An explicit escape character @"FlameCsv.CsvOptions`1.Escape?displayProperty=nameWithType" can be set to a non-null value to escape _any_ character following the escape character. The default value is null, which follows the RFC 4180 spec and wraps values in strings, and escapes quotes with another quote.
Any field containing the escape character **must** be wrapped in quotes. The escape character itself is escaped by doubling it, e.g., `"\\"`.

> [!TIP]
> Due to the rarity of this non-standard format, SIMD accelerated parsing is not supported when using an escape character.

> [!WARNING]
> For performance reasons, all the dialect characters must be ASCII (value 127 or lower). A runtime exception is thrown if the configured dialect contains non-ASCII characters.

### Additional info

Internally, FlameCsv uses the @"FlameCsv.CsvDialect`1" struct to handle the configured dialect. It is constructed from the options when they are used (this makes the options immutable), and contains the configured values and other things related to parsing, such as @"System.Buffers.SearchValues`1" used internally in parsing.

```cs
CsvOptions<byte> options = new()
{
    Delimiter = '\t',
    Quote = '"',
    Newline = "\r\n",
    Trimming = CsvFieldTrimming.Both,
    Escape = '\\',
};
```

## Header

The @"FlameCsv.CsvOptions`1.HasHeader?displayProperty=nameWithType" property is `true` by default, which expects a header record on the first line/record. Header names are matched using the @"FlameCsv.CsvOptions`1.Comparer"-property, which defaults to @"System.StringComparer.OrdinalIgnoreCase?displayProperty=nameWithType".

For more information on which methods transcode the data into @"System.String", see [Transcoding](#transcoding).

```cs
const string csv = "id,name\n1,Bob\n2,Alice\n";

List<User> users = CsvReader.Read(csv, new CsvOptions<char> { HasHeader = true });
```

## Parsing and formatting fields

See @"converters" for an overview on converter configuration, implementation, and what converters are supported by default.

## Quoting fields when writing

The @"FlameCsv.Writing.CsvFieldQuoting" enumeration and @"FlameCsv.CsvOptions`1.FieldQuoting?displayProperty=nameWithType" property are used to configure the behavior when writing CSV. The default, @"FlameCsv.Writing.CsvFieldQuoting.Auto?displayProperty=nameWithType" only quotes fields if they contain special characters or whitespace.

```cs
// quote all fields, e.g., for noncompliant 3rd party libraries
StringBuilder result = CsvWriter.WriteToString(
    [new User(1, "Bob", true)],
    new CsvOptions<char>() { FieldQuoting = CsvFieldQuoting.Always });

// "id","name","isadmin"
// "1","Bob","true"
```

If you are 100% sure your data does not contain any special characters, you can set it to @"FlameCsv.Writing.CsvFieldQuoting.Never?displayProperty=nameWithType" to squeeze out a little bit of performance by omitting the check if each written field needs to be quoted.


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
> Comments are not yet fully supported by FlameCSV (see [issue](https://github.com/ovska/FlameCsv/issues/20)).
For example, even if you configure the callback to skip rows that start with `#`, the rows are still parsed and expected to be properly structured CSV (e.g., no unbalanced quotes). 


## Field count validation

@"FlameCsv.CsvOptions`1.ValidateFieldCount?displayProperty=nameWithType" can be used to validate the field count both when reading and writing.

When reading @"FlameCsv.CsvValueRecord`1", setting the property to `true` ensures that all records have the same field count as the first record.
The expected field count is reset if you [reset the headers with a callback](#skipping-records-or-resetting-headers).

This property also ensures that all records written with @"FlameCsv.CsvWriter`1" have the same field count.
Alternatively, you can use the @"FlameCsv.CsvAsyncWriter`1.ExpectedFieldCount"-property. The property can also be used to reset the expected count by setting it to `null`,
for example when writing multiple CSV documents into one output.

## Advanced topics

### NativeAOT

Since any implementation of @"FlameCsv.CsvConverterFactory`1" (including built-in nullable and enum factories) can potentially require unreferenced types or dynamic code, the default @"FlameCsv.CsvOptions`1.GetConverter``1?displayProperty=nameWithType" method is not AOT-compatible.

Use @"FlameCsv.CsvOptions`1.Aot?displayProperty=nameWithType" to retrieve a wrapper around the configured converters, which provides convenience methods to safely retrieve converters for types known at runtime. See the documentation on methods of @"FlameCsv.CsvOptions`1.AotSafeConverters" for more info. This property is used by the source generator.

```cs
// aot-safe default nullable and enum converters if not configured by user
CsvConverter<char, int?> c1 = options.Aot.GetOrCreateNullable(static o => o.Aot.GetConverter<int>());
CsvConverter<char, DayOfWeek> c2 = options.Aot.GetOrCreateEnum<DayOfWeek>();
```

### Transcoding

The following methods are used by the library to convert `T` values to @"System.Char?text=char" and back:

 - @"FlameCsv.CsvOptions`1.TryGetChars(System.ReadOnlySpan{`0},System.Span{System.Char},System.Int32@)" used to convert the header fields to strings
 - @"FlameCsv.CsvOptions`1.GetAsString(System.ReadOnlySpan{`0})" used in error messages, and to convert long header fields to strings
 - @"FlameCsv.CsvOptions`1.TryWriteChars(System.ReadOnlySpan{System.Char},System.Span{`0},System.Int32@)" used when writing text values, and initializing @"FlameCsv.CsvDialect`1.Newline" and @"FlameCsv.CsvDialect`1.Whitespace"
 - @"FlameCsv.CsvOptions`1.GetFromString(System.String)" used in some converters, and while initializing the dialect

> [!NOTE]
> The library maintains a small pool of @"System.String"-instances of previously encountered headers, so unless your data is exceptionally varied, the allocation cost is paid only once.

> [!WARNING]
> While you can inherit the options-type and override these methods, the library expects both of the the @"System.String" and @"System.ReadOnlySpan`1" methods to return the same sequences for the same inputs. Make sure you override both transcoding methods in either direction, and keep the implementations in sync.<br/>
> Most likely you can achieve the same goals easier by using @"FlameCsv.CsvOptions`1.Comparer" and [custom converters](examples.md#converters).

### Memory pooling

You can configure the @"System.Buffers.MemoryPool`1" instance used internally with the @"FlameCsv.CsvOptions`1.MemoryPool?displayProperty=nameWithType" property. Pooled memory is used to handle escaping, unescaping, and records split across multiple sequence segments. The default value is @"System.Buffers.MemoryPool`1.Shared?displayProperty=nameWithType".

If set to `null`, no pooled memory is used and all temporary buffers are heap allocated.

Further reading: @"architecture".

### Custom binding

If you don't want to use the built-in @"FlameCsv.Binding.CsvReflectionBinder`1" (attribute configuration), set @"FlameCsv.CsvOptions`1.TypeBinder?displayProperty=nameWithType" property to your custom implementation implementing @"FlameCsv.Binding.ICsvTypeBinder`1".
