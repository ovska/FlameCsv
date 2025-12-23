<p align="center">
  <img
    width="128"
    height="128"
    title="FlameCsv logo"
    src="docs/data/logo.png" />
  <h1 align="center">FlameCsv</h1>
  <p align="center">High-performance RFC 4180-compliant CSV library for .NET 10 with trimming/AOT support</p>
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
  - Simple API for reading and writing CSV
  - Built-in support for common CLR types
  - Supports both synchronous and asynchronous operations
  - Flexible; read or write almost any data source
  - Automatic newline detection
  - UTF-8/ASCII support to read/write bytes directly without additional transcoding
  - Supports hot reload
- **High Performance**
  - Optimized for speed and low memory usage
  - SIMD-accelerated parsing routines with hardware intrinsics
  - Batteries-included internal caching and memory pooling for near-zero allocations
  - Reflection code paths that rival manually written code in performance
- **Deep Customization**
  - Read or write either .NET objects, or raw CSV records and fields
  - Attribute configuration for header names, constructors, field order, and more
  - Support for custom converters and converter factories
  - Read or write multiple CSV documents from/to a single data stream
- **Source Generator**
  - Fully annotated and compatible with NativeAOT
  - Supports trimming to reduce application size
  - View and debug the code instead of opaque reflection
  - Compile-time diagnostics instead of runtime errors

# Examples

## Reading records
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
    // get fields by index or header name
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

foreach (var user in Csv.From(data).Read<User>(options))
{
    Console.WriteLine(user);
}
```

## Writing records
```csharp
User[] data = [new(1, "Bob", DateTime.Now, 42), new(2, "Alice", DateTime.UnixEpoch, null)];
StringBuilder builder = new StringBuilder();
Csv.To(builder).Write(data);
Console.WriteLine(builder);
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

## Reading 5000 records into objects

| Method                | Mean     |  Ratio | Allocated | Alloc Ratio |
|----------------------:|---------:|-------:|----------:|------------:|
| FlameCsv (Reflection) | 2.308 ms |   1.00 |   1.66 MB |        1.00 |
| FlameCsv (SourceGen)  | 2.506 ms |   1.09 |   1.66 MB |        1.00 |
| Sylvan                | 2.570 ms |   1.11 |   2.64 MB |        1.59 |
| RecordParser          | 4.673 ms |   2.02 |   1.93 MB |        1.16 |
| CsvHelper             | 6.424 ms |   2.78 |   3.49 MB |        2.10 |

<img src="docs/data/charts/read_light.svg" alt="Reading 5000 records into .NET objects" />

## Iterating 65535 records without processing all fields

| Method        | Mean      | Ratio | Allocated | Alloc Ratio |
|--------------:|----------:|------:|----------:|------------:|
| FlameCsv      |  3.292 ms |  1.00 |     322 B |        1.00 |
| Sep           |  4.431 ms |  1.35 |    5942 B |       18.45 |
| Sylvan        |  5.014 ms |  1.52 |   42029 B |      130.52 |
| RecordParser  |  6.358 ms |  1.93 | 2584418 B |    8,026.14 |
| CsvHelper     | 34.877 ms | 10.60 | 2789195 B |    8,662.10 |

<img src="docs/data/charts/peek_light.svg" alt="Computing sum of one field from 65535 records" />

## Writing 5000 records

| Method                | Mean     | Ratio | Allocated | Alloc Ratio |
|----------------------:|---------:|------:|----------:|------------:|
| FlameCsv (SourceGen)  | 3.196 ms |  1.00 |     170 B |        1.00 |
| FlameCsv (Reflection) | 3.302 ms |  1.03 |     174 B |        1.02 |
| Sylvan                | 3.467 ms |  1.08 |   33605 B |      197.68 |
| Sep                   | 3.561 ms |  1.11 |  121181 B |      712.83 |
| CsvHelper             | 7.806 ms |  2.44 | 2077347 B |   12,219.69 |
| RecordParser          | 9.245 ms |  2.89 | 8691788 B |   51,128.16 |

<img src="docs/data/charts/write_light.svg" alt="Writing 5000 records" />
