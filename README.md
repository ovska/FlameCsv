<p align="center">
  <img
    width="128"
    height="128"
    title="FlameCsv logo"
    src="docs/data/logo.png" />
  <h1 align="center">FlameCsv</h1>
  <p align="center">High-performance RFC 4180-compliant CSV library for .NET 9/10 with trimming/AOT support</p>
  <p align="center" style="text-decoration:none">
    <a href="https://www.nuget.org/packages/FlameCsv/" target="_blank" style="text-decoration:none">
      <img src="https://img.shields.io/nuget/v/FlameCsv" alt="NuGet version" style="text-decoration:none"/>
    </a>
  </p>
</p>

---

- [Download on nuget](https://www.nuget.org/packages/FlameCsv/)
- [See the documentation](https://ovska.github.io/FlameCsv/)

# Features
- **TL;DR:** Blazingly fast, trimmable and easy-to-use feature-rich CSV library
- **Ease of Use**
  - Fluent API to read/write CSV from/to almost any source/destination
  - Built-in support for common CLR types and interfaces like I(Utf8)SpanParsable
  - Full feature parity with sync and async APIs
  - UTF-8/ASCII support to read/write bytes directly from a stream without additional transcoding
  - Hot reload support for internal caches
- **High Performance**
  - SIMD parsers tuned for each platform (AVX2, AVX512, ARM64)
  - Near-zero allocations
  - Parallel APIs to read/write records ordered or unordered with multiple threads
  - Low-level APIs to handle raw CSV field spans directly
- **Deep Customization**
  - Attribute configuration for header names, constructors, field order, etc.
  - Support for custom converters and converter factories (like System.Text.Json)
  - Read or write multiple CSV documents from/to a single data stream
- **Source Generators**
  - Library is fully annotated for NativeAOT and trimming
  - Source generated type maps for reflection-free reading and writing
  - Source generated enum converters with up to 10x better performance than Enum.TryParse/TryFormat

# Examples

```csharp
record User(int Id, string Name, DateTime LastLogin, int? Age = null);

string data = "id,name,lastlogin\n1,Bob,2010-01-01\n2,Alice,2024-05-22";

foreach (var user in Csv.From(data).Read<User>())
{
    Console.WriteLine(user);
}
```

## Reflection-free reading using source generator
```csharp
record User(int Id, string Name, DateTime LastLogin, int? Age = null);

[CsvTypeMap<char, User>]
partial class UserTypeMap;

foreach (var user in Csv.From(data).Read<User>(UserTypeMap.Default))
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
    IgnoreHeaderCase = true,
    Converters = { new CustomDateTimeConverter() },
};

foreach (CsvRecord<char> record in Csv.From(data).Enumerate(options))
{
    var u1 = new User(
        Id:        record.GetField<int>("Id"),
        Name:      record.GetField<string>("Name"),
        LastLogin: record.GetField<DateTime>("LastLogin"),
        Age:       record.FieldCount >= 3 ? record.GetField<int?>("Age") : null);

    // fields can also be fetched via index
    var u2 = new User(
        Id:        record.GetField<int>(0),
        Name:      record.GetField<string>(1),
        LastLogin: record.GetField<DateTime>(2),
        Age:       record.FieldCount >= 3 ? record.GetField<int?>(3) : null);
    
    // or get them once and reuse
    int nameIndex = record.Header["Name"];
}
```

## Writing records
```csharp
// write to a stringbuilder
StringBuilder builder = new StringBuilder();
Csv.To(builder).Write(data);

// write to stream
await Csv.To(stream).WithUtf8Encoding().WriteAsync(data);

// write to file in parallel
Csv.ToFile("output.csv").AsParallel(cancellationToken).Write(data);
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

# Benchmarks

See detailed benchmarks in the [documentation page](https://ovska.github.io/FlameCsv/docs/benchmarks.html).
