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
var config = CsvConfiguration<byte>.Default;
await foreach (User record in CsvReader.ReadAsync<User>(config, File.OpenRead("/home/ovska/test.csv"))
{
    // ...
}
```

## Binding to column indexes
```csv
1,Bob
2,Alice
```
```csharp
class User
{
    [CsvIndexBinding(0)] public int Id { get; set; }
    [CsvIndexBinding(1)] public string? Name { get; set; }
}
```
```csharp
var config = CsvConfiguration<byte>.DefaultBuilder
    .SetBinder(new IndexBindingProvider<byte>())
    .Build();
await foreach (User record in CsvReader.ReadAsync<User>(config, File.OpenRead("/home/ovska/test.csv"))
{
    // ...
}
```

## Reading from a string
```csharp
var csv = @"Id,Name\n1,Bob\n2,Alice";
var config = CsvConfiguration<char>.Default;
await foreach (User record in CsvReader.ReadAsync<User>(config, new StringReader(csv)))
{
    // ...
}
```

work in progress..
