---
uid: examples
---

# Examples

## TL;DR

Use @"FlameCsv.CsvReader" to read CSV records or .NET objects, and @"FlameCsv.CsvWriter" to write them.

Here's the example class used throughout this documentation:

```cs
class User
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public bool IsAdmin { get; set; }
}
```

## Customizing the CSV dialect

Configure the CSV format using these @"FlameCsv.CsvOptions`1" properties:
- @"FlameCsv.CsvOptions`1.Delimiter"
- @"FlameCsv.CsvOptions`1.Quote"
- @"FlameCsv.CsvOptions`1.Newline"
- @"FlameCsv.CsvOptions`1.Trimming"
- @"FlameCsv.CsvOptions`1.Escape"

Example for reading semicolon-delimited CSV with linefeed separators and space/tab trimming:

```cs
CsvOptions<char> options = new()
{
    Delimiter = ';',
    Quote = '"',
    Newline = "\n",
    Trimming = CsvFieldTrimming.Both,
};
```

For more details, see @"configuration#dialect". The configuration is identical between @"System.Char?text=char" and @"System.Byte?text=byte"; the options-instance internally converts the UTF16 values into UTF8.

## Reading objects

### .NET types

Use the static @"FlameCsv.CsvReader" class for reading CSV data. The `Read`-methods accept various data sources and return an object that can be used with `foreach`, `await foreach`, or LINQ. When `options` is omitted (or `null`), @"FlameCsv.CsvOptions`1.Default?displayProperty=nameWithType" is used.

The library supports both @"System.Char?text=char" (UTF-16) and @"System.Byte?text=byte" (UTF-8) data through C# generics. For bytes, the library expects UTF-8 encoded text (which includes ASCII).

CSV can be read from numerous data sources: @"System.String", @"System.ReadOnlyMemory`1", and @"System.Buffers.ReadOnlySequence`1".

CSV can also be streamed synchronously or asynchronously from @"System.IO.TextReader" and @"System.IO.Stream".
When streaming, individual fields are tokenized from the input stream. Individual records and fields (including escaping)
are not materialized until accessed, and the library will not buffer the entire CSV data in memory.

# [UTF-16](#tab/utf16)
```cs
// sync
string csv = "id,name,isadmin\r\n1,Bob,true\r\n2,Alice,false\r\n";
List<User> users = CsvReader.Read<User>(csv).ToList();

// async
await foreach (var user in CsvReader.Read<User>(csv))
{
    ProcessUser(user);
}
```

# [UTF-8](#tab/utf8)
```cs
// sync
byte[] csv = "id,name,isadmin\r\n1,Bob,true\r\n2,Alice,false\r\n"u8.ToArray();
List<User> users = CsvReader.Read<User>(csv).ToList();

// async
await foreach (var user in CsvReader.Read<User>(csv))
{
    ProcessUser(user);
}
```

---

> [!WARNING]
> The enumerator objects must be disposed after use to properly clean up internal objects and pooled buffers. This can be done implicitly by using them in a `foreach`, `using`, or LINQ statement, or explicitly with @"System.IDisposable.Dispose" or @"System.IAsyncDisposable.DisposeAsync".

### CSV records and fields

The @"FlameCsv.CsvReader" class contains the `Enumerate`-methods to read @"FlameCsv.CsvRecord`1" that wraps the CSV data, and can be used to inspect details about the records, such has field counts, unescaped record or field, line numbers, and exact character/byte offsets in the file.

Fields can be accessed directly using the @"FlameCsv.CsvFieldIdentifier"-struct, which is implicitly convertible from @"System.String" and @"System.Int32". In the example below, passing `"id"` and `0` to `ParseField` are functionally equivalent. The string-overload however includes and additional array/dictionary-lookup. The @"FlameCsv.CsvRecord`1.Header" and @"FlameCsv.CsvRecord`1.FieldCount" properties can be used to determine if a specific field can be accessed, along with @"FlameCsv.CsvRecord`1.Contains(FlameCsv.CsvFieldIdentifier)".

The @"FlameCsv.CsvRecord`1" struct can also be used in a `foreach`-statement to iterate the escaped field values.

# [Type binding](#tab/example-types)
```cs
foreach (ref readonly CsvRecord<char> record in CsvReader.Enumerate(csv))
{
    Console.WriteLine("Fields: {0}",         record.FieldCount);
    Console.WriteLine("Line in file: {0}",   record.Line);
    Console.WriteLine("Start position: {0}", record.Position);

    yield return record.ParseRecord<User>();
}
```

# [Manual](#tab/example-manual)
```cs
foreach (ref readonly CsvRecord<char> record in CsvReader.Enumerate(csv))
{
    Console.WriteLine("Fields: {0}",         record.FieldCount);
    Console.WriteLine("Line in file: {0}",   record.Line);
    Console.WriteLine("Start position: {0}", record.Position);

    yield return new User
    {
        Id = record.ParseField<int>("id"),
        Name = record.ParseField<string?>("name"),
        IsAdmin = record.ParseField<bool>("isadmin"),
    };

    // or
    yield return new User
    {
        Id = record.ParseField<int>(0),
        Name = record.ParseField<string?>(1),
        IsAdmin = record.ParseField<bool>(2),
    };
}
```

---

> [!WARNING]
> A @"FlameCsv.CsvRecord`1" instance is only valid until `MoveNext()` is called on the enumerator. The struct is a thin wrapper around the actual data, and may use invalid or pooled memory if used after its intended lifetime. A runtime exception will be thrown if it is accessed after the enumeration has continued or ended. See @"FlameCsv.CsvPreservedRecord`1" for an alternative.

### Reading raw CSV data

For advanced performance-critical scenarios, you can create a @"FlameCsv.Reading.CsvReader`1".
This is the type that is used internally to tokenize the CSV data. @"FlameCsv.Reading.CsvReader`1.ParseRecords" and @"FlameCsv.Reading.CsvReader`1.ParseRecordsAsync(System.Threading.CancellationToken)" can be used to read the tokenized CSV (field and record spans).

# [UTF-16](#tab/utf16)
```cs
foreach (CsvRecordRef<char> record in new CsvReader<char>(CsvOptions<char>, textReader).ParseRecords())
{
    for (int i = 0; i < record.FieldCount; i++)
    {
        ReadOnlySpan<char> f = record[i]; // or record.GetRawSpan(i) to skip unescaping
        // use the field
    }
}
```

# [UTF-8](#tab/utf8)
```cs
foreach (CsvRecordRef<byte> record in new CsvReader<byte>(CsvOptions<byte>, stream).ParseRecords())
{
    for (int i = 0; i < record.FieldCount; i++)
    {
        ReadOnlySpan<byte> f = record[i]; // or record.GetRawSpan(i) to skip unescaping
        // use the field
    }
}
```
---

Only one field may be read at a time, as unescaping the fields uses a shared buffer, and you should process fields one by one before reading the next one.
Fields that don't need escaping or are fetched with `GetRawSpan` can be handled concurrently.

## Writing

### .NET types

The @"FlameCsv.CsvWriter" class provides methods to write .NET objects as CSV records. Supported outputs include:
- Files
- @"System.IO.Stream"
- @"System.IO.TextWriter"
- @"System.Text.StringBuilder"
- @"System.IO.Pipelines.PipeWriter"

You can write both @"System.Collections.Generic.IEnumerable`1" and @"System.Collections.Generic.IAsyncEnumerable`1" data.

```cs
User[] users =
[
    new User { Id = 1, Name = "Bob", IsAdmin = true },
    new User { Id = 2, Name = "Alice", IsAdmin = false },
];

CsvWriter.Write(TextWriter.Null, users);
```

### CSV records and fields

@"FlameCsv.CsvWriter" includes a `Create` method that can be used to create an
instance of @"FlameCsv.CsvWriter`1" that allows you to write fields, records,
or unescaped raw data directly into your output, while taking care of field
quoting, delimiters, and escaping. The writer can be flushed manually, or
be configured to flush automatically if the library detects that the internal buffers are getting saturated.

# [Type binding](#tab/example-types)
```cs
using (CsvWriter<char> writer = CsvWriter.Create(TextWriter.Null))
{
    writer.WriteHeader<User>();
    writer.NextRecord();

    writer.WriteRecord(new User { Id = 1, Name = "Bob", IsAdmin = true });
    writer.NextRecord();

    writer.Complete(exception: null);
}
```

# [Manual](#tab/example-manual)
```cs
using (CsvWriter<char> writer = CsvWriter.Create(TextWriter.Null))
{
    writer.WriteHeader("id", "name", "isadmin");
    writer.NextRecord();

    writer.WriteField(1);
    writer.WriteField("Bob"); // you can use "Bob"u8 when writing directly as UTF-8
    writer.WriteField(true);
    writer.NextRecord();

    writer.Complete(exception: null);
}
```
---

After writing, @"FlameCsv.CsvWriter`1.Complete(System.Exception)" or @"FlameCsv.CsvWriter`1.CompleteAsync(System.Exception,System.Threading.CancellationToken)" should be called to flush buffered data and properly dispose of resources used by the writer instance.

> [!NOTE]
> The @"System.Exception" parameter is used to suppress flushing any remaining data if the write operation errored. A `using`-statement or `Dispose` can be used to clean up the writer instance similarly, but you lose the aforementioned benefit by only disposing. You can safely wrap a manually completed writer in a `using` block, since multiple completions are harmless.

## CSV without a header

To read or write headerless CSV:
1. Annotate types with @"FlameCsv.Attributes.CsvIndexAttribute"
2. Set @"FlameCsv.CsvOptions`1.HasHeader?displayProperty=nameWithType" to `false`


```cs
class User
{
    [CsvIndex(0)] public int Id { get; set; }
    [CsvIndex(1)] public string? Name { get; set; }
    [CsvIndex(2)] public bool IsAdmin { get; set; }
}

CsvOptions<char> options = new() { HasHeader = false };

const string csv =
    """
    1,Bob,true
    2,Alice,false
    """;

foreach (User user in CsvReader.Read<User>(csv, options))
{
    ProcessUser(user);
}
```

See @"attributes#headerless-csv" for more details on how to customize the binding rules.

## Converters

The converters in FlameCsv follow the common .NET pattern TryParse/TryFormat.

When reading CSV, @"FlameCsv.CsvConverter`2.TryParse(System.ReadOnlySpan{`0},`1@)" is used to convert the CSV field into a .NET type instance. If parsing fails the converter returns `false` and the library throws an appropriate exception.

When writing, @"FlameCsv.CsvConverter`2.TryFormat(System.Span{`0},`1,System.Int32@)" should attempt to write the value to the destination buffer. If the value was successfully written, the method returns `true` and sets the amount of written characters (or bytes). If the destination buffer is too small, the method returns `false`. In this case, the value of `charsWritten` and any data possibly already written to the buffer are ignored.

### Custom converter

The following example implements a converter that writes and reads booleans as `"yes"` or `"no"` (case insensitive).

# [UTF-16](#tab/utf16)
```cs
class YesNoConverter : CsvConverter<char, bool>
{
    public override bool TryParse(ReadOnlySpan<char> source, out bool value)
    {
        if (source.Equals("yes", StringComparison.OrdinalIgnoreCase))
        {
            value = true;
            return true;
        }
        else if (source.Equals("no", StringComparison.OrdinalIgnoreCase))
        {
            value = false;
            return true;
        }
        else
        {
            value = default;
            return false;
        }
    }

    public override bool TryFormat(Span<char> destination, bool value, out int charsWritten)
    {
        string toWrite = value ? "yes" : "no";
        charsWritten = toWrite.Length; // charsWritten is ignored if the method returns false
        return toWrite.TryCopyTo(destination);
    }
}
```

# [UTF-8](#tab/utf8)
```cs
class YesNoConverter : CsvConverter<byte, bool>
{
    public override bool TryParse(ReadOnlySpan<byte> source, out bool value)
    {
        if (Ascii.EqualsIgnoreCase(source, "yes"u8))
        {
            value = true;
            return true;
        }
        else if (Ascii.EqualsIgnoreCase(source, "no"u8))
        {
            value = false;
            return true;
        }
        else
        {
            value = default;
            return false;
        }
    }

    public override bool TryFormat(Span<byte> destination, bool value, out int charsWritten)
    {
        ReadOnlySpan<byte> toWrite = value ? "yes"u8 : "no"u8;
        charsWritten = toWrite.Length; // charsWritten is ignored if the method returns false
        return toWrite.TryCopyTo(destination);
    }
}
```
---

### Converter factory

This example implements a factory that creates a generic @"System.Collections.Generic.IEnumerable`1" converter
that reads the item values separated by `;` (semicolon).

Thanks to the @"System.Numerics.IBinaryInteger`1"-constraint,
we can create the separator for both token without having to resort to `typeof`-checks and/or casts/unsafe.
Alternatively, the factory could pass the separator as a constructor parameter.

```cs
CsvOptions<byte> options = new() { Converters = { new EnumerableConverterFactory<byte>() } };

// reads and writes values in the form of "1;2;3"
var converter = options.GetConverter<IEnumerable<int>>();

class EnumerableConverterFactory<T> : CsvConverterFactory<T>
    where T : unmanaged, System.Numerics.IBinaryInteger<T>
{
    public override bool CanConvert(Type type)
    {
        return IsIEnumerable(type) || type.GetInterfaces().Any(IsIEnumerable);
    }

    public override CsvConverter<byte> Create(Type type, CsvOptions<T> options)
    {
        var elementType = type.GetInterfaces().First(IsIEnumerable).GetGenericArguments()[0];
        return (CsvConverter<T>)Activator.CreateInstance(
            typeof(EnumerableConverterFactory<,>).MakeGenericType(typeof(T), elementType),
            args: [options])!;
    }

    private static bool IsIEnumerable(Type type)
        => type.IsInterface &&
           type.IsGenericType &&
           type.GetGenericTypeDefinition() == typeof(IEnumerable<>);
}

class EnumerableConverterFactory<T, TElement>(CsvOptions<T> options) : CsvConverter<T, IEnumerable<TElement>>
    where T : unmanaged, System.Numerics.IBinaryInteger<T>
{
    private readonly CsvConverter<T, TElement> _elementConverter = options.GetConverter<TElement>();

    public override bool TryParse(
        ReadOnlySpan<T> source,
        [MaybeNullWhen(false)] out IEnumerable<TElement> value)
    {
        if (source.IsEmpty)
        {
            value = [];
            return true;
        }

        List<TElement> result = [];

        foreach (Range range in source.Split(T.CreateTruncating(';')))
        {
            if (!_elementConverter.TryParse(source[range], out var element))
            {
                value = null;
                return false;
            }

            result.Add(element);
        }

        value = result;
        return true;
    }

    public override bool TryFormat(
        Span<T> destination,
        IEnumerable<TElement> value,
        out int charsWritten)
    {
        // keep track of total characters written
        charsWritten = 0;
        bool first = true;

        foreach (var element in value)
        {
            if (first)
            {
                first = false;
            }
            else
            {
                if (charsWritten >= destination.Length)
                {
                    return false;
                }

                destination[charsWritten++] = T.CreateTruncating(';');
            }
            
            if (!_elementConverter.TryFormat(destination.Slice(charsWritten), element, out int written))
            {
                return false;
            }

            charsWritten += written;
        }

        return true;
    }
}
```
---

Note how the @"System.Numerics.IBinaryInteger`1" constraint allows us to use the same character for both UTF16 and UTF8.
