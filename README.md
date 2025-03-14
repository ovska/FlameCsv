<p align="center">
  <img
    width="128"
    height="128"
    title="FlameCsv logo"
    src="docs/data/logo.png" />
  <h1 align="center">FlameCsv</h1>
  <p align="center">High-performance RFC 4180-compliant CSV library for .NET 9 with trimming/AOT support</p>
</p>

# Features
- **TL;DR:** Blazingly fast, trimmable and easy-to-use feature-rich CSV library
- 💡 **Ease of Use**
  - Simple API for reading and writing CSV
  - Built-in support for common CLR types
  - Supports both synchronous and asynchronous operations
  - Flexible; read or write almost any data source
  - Automatic newline detection
  - UTF-8/ASCII support to read/write bytes directly without additional transcoding
  - Supports hot reload
- 🚀 **High Performance**
  - Optimized for speed and low memory usage
  - SIMD-accelerated parsing routines with hardware intrinsics
  - Batteries-included internal caching and memory pooling for near-zero allocations
  - Reflection code paths that rival manually written code in performance
- 🛠️ **Deep Customization**
  - Read or write either .NET objects, or raw CSV records and fields
  - Attribute configuration for header names, constructors, field order, and more
  - Support for custom converters and converter factories
  - Read or write multiple CSV documents from/to a single data stream
- ✍️ **Source Generator**
  - Fully annotated and compatible with NativeAOT
  - Supports trimming to reduce application size
  - View and debug the code instead of opaque reflection
  - Compile-time diagnostics instead of runtime errors

# Examples

## Reading records
```csharp
record User(int Id, string Name, DateTime LastLogin, int? Age = null);

string data = "id,name,lastlogin\n1,Bob,2010-01-01\n2,Alice,2024-05-22";

foreach (var user in CsvReader.Read<User>(data))
{
    Console.WriteLine(user);
}
```

## Reflection-free reading using source generator
```csharp
record User(int Id, string Name, DateTime LastLogin, int? Age = null);

[CsvTypeMap<char, User>]
partial class UserTypeMap;

foreach (var user in CsvReader.Read<User>(data, UserTypeMap.Default))
{
    Console.WriteLine(user);
}
```

## Reading fields manually
```csharp
string data = "id,name,lastlogin,age\n1,Bob,2010-01-01,42\n2,Alice,2024-05-22,\n";

CsvOptions<char> options = new()
{
    HasHeader = true,
    Comparer = StringComparer.Ordinal,
    Converters = { new CustomDateTimeConverter() },
};

foreach (CsvValueRecord<char> record in CsvReader.Enumerate(data, options))
{
    // get fields by column index or header name
    var u1 = new User(
        Id:        record.GetField<int>("Id"),
        Name:      record.GetField<string>("Name"),
        LastLogin: record.GetField<DateTime>("LastLogin"),
        Age:       record.FieldCount >= 3 ? record.GetField<int?>("Age") : null);

    var u2 = new User(
        Id:        record.GetField<int>(0),
        Name:      record.GetField<string>(1),
        LastLogin: record.GetField<DateTime>(2),
        Age:       record.FieldCount >= 3 ? record.GetField<int?>(3) : null);
}
```

## Reading headerless CSV
```csharp
class User
{
    [CsvIndex(0)] public int Id { get; set; }
    [CsvIndex(1)] public string? Name { get; set; }
    [CsvIndex(2)] public DateTime LastLogin { get; set; }
}

string data = "1,Bob,2010-01-01\n2,Alice,2024-05-22";

var options = new CsvOptions<char> { HasHeader = false };

foreach (var user in CsvReader.Read<User>(data, options))
{
    Console.WriteLine(user);
}
```

## Writing records
```csharp
User[] data = [new(1, "Bob", DateTime.Now, 42), new(2, "Alice", DateTime.UnixEpoch, null)];
StringBuilder result = CsvWriter.WriteToString(data);
Console.WriteLine(result);
```

## Writing manually
```csharp
using (CsvWriter<char> writer = CsvWriter.Create(TextWriter.Null))
{
    writer.WriteRecord(new User(1, "Bob", DateTime.Now, 42));
    writer.NextRecord();
    writer.Flush();

    writer.WriteField(1);
    writer.WriteField("Alice");
    writer.WriteField(DateTime.UnixEpoch);
    writer.WriteField((int?)null);
    writer.NextRecord();
}
```
