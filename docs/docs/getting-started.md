---
uid: getting-started
---

# Getting Started

## Installation

Install [FlameCsv](https://www.nuget.org/packages/FlameCsv) using the NuGet package manager:

```shell
dotnet add package FlameCsv
```

## Basic Usage

Reading CSV data is as simple as:

```cs
// Reading from a string
IEnumerable<User> users = Csv.From("id,name\n1,John\n2,Jane").Read<User>();

// Reading from a file
await foreach (User user in Csv.FromFile("users.csv").ReadAsync<User>(cancellationToken))
{
    Console.WriteLine(user.Name);
}

// Peeking fields directly from the underlying data
double sum = 0;

foreach (CsvRecordRef<byte> record in new CsvReader<byte>(options, (ReadOnlyMemory<byte>)csv).ParseRecords())
{
    ReadOnlySpan<byte> field = record[3];

    if (double.TryParse(field, out double value))
    {
        sum += value;
    }
}
```

Writing CSV is just as easy:

```cs
var users = new[]
{
    new User { Id = 1, Name = "John" },
    new User { Id = 2, Name = "Jane" }
};

// Writing to a string
StringBuilder destination = new();
Csv.To(destination).Write(users);
return destination.ToString();

// Writing to a file
await Csv.ToFile("users.csv").WriteAsync(users, cancellationToken);
```

## Configuration

FlameCsv is highly configurable. Common options include:
- CSV dialect (delimiters, quotes) - see @"configuration#dialect"
- Header mapping - see @"attributes#header-names"
- Type conversion - see @"configuration#parsing-and-formatting-fields"

Example of custom configuration:

```cs
CsvOptions<char> options = new()
{
    Delimiter = ';',
    Quote = '"',
    Trimming = CsvFieldTrimming.Leading,
    HasHeader = true,
    Comparer = StringComparer.Ordinal,
};
```

The configuration object is identical for `char` and `byte`, and UTF16 <-> UTF8 conversion is handled automatically.
Only differences are the converters, which are separate for `char` and `byte`.

## Next Steps

- See @"examples" for more detailed examples
- Learn about configuring types in @"attributes"
- Browse available configuration options in @"configuration"
- Check out the performance @"benchmarks"
