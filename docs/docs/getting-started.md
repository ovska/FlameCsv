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
IEnumerable<User> users = CsvReader.Read<User>("id,name\n1,John\n2,Jane");

// Reading from a file
await foreach (User user in CsvReader.ReadFromFile<User>("users.csv").WithCancellation(cancellationToken))
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
StringBuilder csv = CsvWriter.WriteToString(users);

// Writing to a file
await CsvWriter.WriteToFileAsync("users.csv", users, cancellationToken);
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
- Understand the internals and design philosophy in @"architecture"

## Comparisons to other libraries

|                     | FlameCsv    | CsvHelper          | Sylvan     | Sep        | RecordParser |
| ------------------- | ----------- | ------------------ | ---------- | ---------- | ------------ |
| License             | Apache 2.0  | MS-PL / Apache 2.0 | MIT        | MIT        | MIT          |
| Performance         | 🐇 Fast      | 🐌 Slow             | 🐇 Fast     | 🐇 Fast     | 🐟 Moderate   |
| Memory use          | 😎 Near-zero | 🤯 High             | 🤐 Moderate | 😌 Low      | 🤯 High       |
| Async support       | ✔️ Yes       | ✔️ Yes              | ✔️ Yes      | 〽️ Partial | ❌ No         |
| Type binding        | ✔️ Yes       | ✔️ Yes              | ✔️ Yes      | ❌ No       | ❌ No         |
| AOT compatible      | ✔️ Yes       | ❌ No               | ❌ No       | ✔️ Yes      | ❌ No         |
| Broken data support | ❌ No        | ✔️ Yes              | ✔️ Yes      | ✔️ Yes      | ❔ Unknown    |
