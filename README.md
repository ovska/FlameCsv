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
- **Usage**
  - Straightforward API with minimal fiddling unless needed
  - Supports reading both `char` and `byte` (UTF-8)
  - Read from `TextReader`, `Stream`, `PipeReader`, `ReadOnlySequence`, `string`, etc.
  - Write to `StringBuilder`, `TextWriter`, `Stream`, file, etc.
  - Converter API similar to System.Text.Json
  - Provides low-level types to manually parse CSV
- **Data binding**
  - Supports both binding to classes/structs and reading records and individual fields manually
  - Supports binding to CSV headers, or column indexes
  - Supports complex object initialization, e.g. a combination of properties and constructor params
- **Configuration**
  - Easy configuration for converting numbers, dates and many other built-in types
  - Supports both RFC4180/Excel and Unix/escaped CSV styles
  - Automatic newline detection between `\n` and `\r\n`
- **Performance**
  - Built with performance in mind from the ground up
  - Minimal allocations when reading records asynchronously
  - Near-zero allocations when reading records synchronously
  - Near-zero allocations when enumerating CSV (e.g. peeking fields)
- **Source generator**
  - NativeAOT / trimming compatible
  - Same feature list and API as reflection based binding

# Examples

## Reading records
```csharp
record User(int Id, string Name, DateTime LastLogin, int? Age = null);

string data = "id,name,lastlogin\n1,Bob,2010-01-01\n2,Alice,2024-05-22";

foreach (var user in CsvReader.Read<User>(data))
{
    Console.WriteLine(user);
}

await foreach (var user in CsvReader.ReadAsync<User>(new TextReader(data)))
{
    Console.WriteLine(user);
}
```

## Reading UTF8 directly from bytes
```csharp
var options = new CsvOptions<byte> { FormatProvider = CultureInfo.CurrentCulture };

await foreach (var user in CsvReader.ReadAsync<User>(File.OpenRead(@"C:\test.csv"), options))
{
    Console.WriteLine(user);
}
```

## Reflection-free reading using source generator (for NativeAOT/trimming)
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

var options = new CsvOptions<char> {
  Comparer = StringComparer.Ordinal,
  Converters = { new CustomDateTimeConverter() }
};

foreach (CsvValueRecord<char> record in CsvReader.Enumerate(data, options))
{
    // get fields by column index or header name
    var u1 = new User(
        Id:        record.GetField<int>("Id"),
        Name:      record.GetField<string>("Name"),
        LastLogin: record.GetField<DateTime>("LastLogin"),
        Age:       record.GetFieldCount() >= 3 ? record.GetField<int?>("Age") : null);

    var u2 = new User(
        Id:        record.GetField<int>(0),
        Name:      record.GetField<string>(1),
        LastLogin: record.GetField<DateTime>(2),
        Age:       record.GetFieldCount() >= 3 ? record.GetField<int?>(3) : null);
}
```

## Copy CSV structure into objects for later use
```csharp
string data = "id,name,lastlogin,age\n1,Bob,2010-01-01,42\n2,Alice,2024-05-22,\n";

// CsvRecord reference type copies the CSV fields for later use
List<CsvRecord<char>> records = [.. CsvReader.Enumerate(data).Preserve()];
Console.WriteLine("First name: " + records[0].GetField(1));
return records[^1];
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
User[] data =
[
    new User(1, "Bob", DateTime.UnixEpoch, 42),
    new User(2, "Alice", DateTime.UnixEpoch, null),
];

StringBuilder result = CsvWriter.WriteToString(data);
Console.WriteLine(result);

await CsvWriter.WriteAsync(data, stream, leaveOpen: true);
```

# Performance

Note: CsvHelper was chosen because it is the most popular CSV library for C#. This library isn't meant to be a replacement for CsvHelper.
Example CSV file used in th benchmarks is found in the `TestData` folder in the tests-project.

---

![image](https://user-images.githubusercontent.com/68028366/235344076-a82ccca6-b3a1-4a00-9509-dcf261aaad06.png)

![image](https://user-images.githubusercontent.com/68028366/235345259-87c1013b-91b4-4d60-bcaf-5924ed467df4.png)

---

![image](https://user-images.githubusercontent.com/68028366/235345278-67fef09b-b742-442b-a680-0e8675e8309c.png)

![image](https://user-images.githubusercontent.com/68028366/235345292-c77d3870-9fc6-456e-9e6b-05effd1300f5.png)

--

![image](https://user-images.githubusercontent.com/68028366/236668926-2e928850-36b8-4610-a50e-168fa56de8e0.png)

![image](https://user-images.githubusercontent.com/68028366/236668945-440fff1a-3b2d-4f57-8f94-6231f62afacc.png)
