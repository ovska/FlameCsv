<p align="center">
  <img
    width="128"
    height="128"
    title="Flames icons created by Flat Icons - Flaticon"
    src="https://user-images.githubusercontent.com/68028366/197605525-a4a8c70f-d757-441b-a26a-adcfaca9ee03.png" />
  <h1 align="center">FlameCsv</h1>
  <p align="center">High-performance RFC 4180-compliant CSV parsing library for .NET 6+</p>
</p>

# Examples

## Reading records with a lambda function
```csharp
var records = CsvReader.ReadRecordsAsync(
    new StringReader("1,true,Bob\r\n2,false,Alice\r\n"),
    CsvTextReaderOptions.Default,
    (int id, bool enabled, string name) => new { id, enabled, name });

await foreach (var record in records)
{
    Console.WriteLine(record);
}
```

## Binding field indexes to classes
```csv
1,Bob
2,Alice
```
```csharp
class User
{
    [CsvIndex(0)] public int Id { get; set; }
    [CsvIndex(1)] public string? Name { get; set; }
}
```
```csharp
var options = CsvUtf8ReaderOptions.Default;
var file = File.OpenRead("/home/ovska/test.csv");
await foreach (var record in CsvReader.ReadAsync<User>(file, options))
{
    // ...
}
```

## Binding to header
```csv
Id,Name
1,Bob
2,Alice
```
```csharp
class User
{
    public int Id { get; set; }
    public string? Name { get; set; }
}
```
```csharp
var options = new CsvUtf8ReaderOptions
{
    options.HasHeader = true;
}
var file = File.OpenRead("/home/ovska/test.csv");

await foreach (User record in CsvReader.ReadAsync<User>(file, options))
{
    // ...
}
```

## Field index binding
```csharp
[CsvIndexIgnore(2)]
record User(
    [CsvIndex(0)] int Id,
    [CsvIndex(1)] string Name,
    bool IsAdmin = false)
{
    [CsvIndex(3)]
    public DateTime Created { get; init; }
}
```

## Reading from buffered data
```csharp
var csv = @"Id,Name\n1,Bob\n2,Alice";
var options = CsvTextReaderOptions.Default;
foreach (User record in CsvReader.Read<User>(csv, options))
{
    // ...
}
```

## Reading manually
```csharp
var csv = @"1,Bob\n2,Alice";
var options = CsvTextReaderOptions.Default;
foreach (CsvRecord<char> record in CsvReader.Enumerate(csv, options))
{
    var user = new User
    {
        Id = record.GetValue<int>(0),
        Name = record.GetValue<string>(1),
    };
}
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
