<p align="center">
  <img
    width="128"
    height="128"
    title="Flames icons created by Flat Icons - Flaticon"
    src="https://user-images.githubusercontent.com/68028366/197605525-a4a8c70f-d757-441b-a26a-adcfaca9ee03.png" />
  <h1 align="center">FlameCsv</h1>
  <p align="center">High-performance RFC 4180-compliant CSV parsing library for .NET 6</p>
</p>

# Examples

## Binding to column indexes
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
await foreach (User record in CsvReader.ReadAsync<User>(File.OpenRead("/home/ovska/test.csv"), options))
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

await foreach (User record in CsvReader.ReadAsync<User>(File.OpenRead("/home/ovska/test.csv"), options))
{
    // ...
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
