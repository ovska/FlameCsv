<p align="center">
  <img
    width="128"
    height="128"
    title="Flames icons created by Flat Icons - Flaticon"
    src="https://user-images.githubusercontent.com/68028366/197605525-a4a8c70f-d757-441b-a26a-adcfaca9ee03.png" />
  <h1 align="center">FlameCsv</h1>
  <p align="center">High-performance RFC 4180-compliant CSV library for .NET 9 with trimming/AOT support</p>
</p>

# Features
- **TL;DR:** Blazingly fast, trimmable and easy-to-use feature-rich CSV library
- **Usage**
  - Straightforward API with sensible defaults but deep configuration
  - Support both sync and async, using either `char` or `byte` (UTF-8)
  - Read from almost any source, including `TextReader`, `Stream`, `PipeReader`, `ReadOnlySequence`, `string`
  - Write to almost any source, including `TextWriter`, `Stream`, `StringBuilder`, `PipeWriter`, file, etc.
  - Options and Converter API similar to System.Text.Json for familiarity
  - Access to raw CSV field slices for high-performance manual processing
- **Data binding**
  - Read classes/structs/records directly, or enumerate records and fields one-by-one
  - Support for headerless CSV
  - Supports complex object initialization patterns such as a mix of constructor parameters and properties
- **Configuration**
  - Built-in support for common .NET types, including enums and nullable value types
  - Supports both RFC4180/Excel and Unix/escaped CSV styles
  - Customizable field separator, quotes, and escape characters
  - Automatic newline detection between `\n` and `\r\n`, or explicit configuration
  - Configure converters on a per-member basis
  - Apply configuration to any type with assembly-targeted attributes
- **Performance**
  - SIMD-accelerated parsing for blazing (or flaming?) fast performance
  - Built with performance in mind from the ground up
  - Near-zero allocations internally when reading and writing CSV
  - Fast manual field reading and writing of whole records or individual fields
  - Benefit from either cached reflection-based or pre-compiled sourcegenerator-based binding
- **Source generator**
  - No reflection or dynamic code needed even for complex type binding patterns
  - All reading and writing APIs have a source-generator equivalent
  - Ideal for NativeAOT and trimming scenarios

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

foreach (var user in CsvReader.Read<User>(data, UserTypeMap.Instance))
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
