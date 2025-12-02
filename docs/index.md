---
_layout: landing
---

# Introduction

🔥 FlameCsv 🔥 is a fully-featured high performance CSV library for .NET with a simple API, deep customization options, and built-in support for UTF8, nativeAOT, and more.

FlameCsv can be thought to be the System.Text.Json of CSV libraries. It is designed to be fast, easy to use, and extensible, while supporting low-level operations for advances use-cases. It is consistently the fastest .NET CSV library in reading, writing, and enumerating CSV data, and does this while also allocating the least memory.

FlameCsv can read CSV at [multiple gigabytes per second](docs/benchmarks.md#reading-without-processing-all-fields) on consumer hardware, and write arbitrarily large amounts of CSV with [near-zero allocations](docs/benchmarks.md#writing-net-objects) irrespective of the dataset's size.
FlameCsv leverages modern .NET patterns such as spans, SIMD hardware intrinsics, memory/string pooling, and buffer writers, and is built from the ground up to provide an easy-to-use high performance experience.

The library has thousands of tests, and critical paths have been fuzz-tested with SharpFuzz.

See @"getting-started", view the @"examples", or deep dive into the @"FlameCsv?text=API Reference".

# Features

- **[Ease of Use](docs/examples.md)**
  - Simple API for reading and writing CSV
  - Built-in support for common CLR types
  - Supports both synchronous and asynchronous operations
  - Flexible; read or write almost any data source
  - UTF-8/ASCII support to read/write bytes directly without additional transcoding
  - Supports hot reload
- **[High Performance](docs/benchmarks.md)**
  - Optimized for speed and low memory usage
  - Specialized SIMD-accelerated parsing and unescaping
  - Batteries-included internal caching and memory pooling for near-zero allocations
  - Reflection code paths that rival and exceed manually written code in performance
- **[Deep Customization](docs/configuration.md)**
  - Read or write either .NET objects, or raw CSV records and fields
  - Attribute configuration for header names, constructors, field order, and more
  - Support for custom converters and converter factories
  - Read or write multiple CSV documents from/to a single data stream
- **[Source Generator](docs/source-generator.md)**
  - Fully annotated and compatible with NativeAOT
  - Supports trimming to reduce application size
  - Debuggable source code instead of compiled expressions
  - Compile-time diagnostics instead of runtime errors
  - Feature parity with reflection-based code paths
  - Enum converter generator for up to 10x faster enum parsing and 7x faster formatting (than Enum.TryParse/Format)

# Example

# [UTF-16](#tab/utf16)
```cs
using FlameCsv;

const string data =
    """
    id,name,age
    1,Bob,42
    2,Alice,37
    3,"Bond, James",39
    """;

List<User> users = [];

// read users from utf16 string
foreach (var user in Csv.From(data).Read<User>())
{
    users.Add(user);
}

// write users to a stream as tab-separated fields
await Csv.To(stream, Encoding.UTF8).WriteAsync(
    users,
    new CsvOptions<char> { Delimiter = '\t' },
    cancellationToken);
```

# [UTF-8](#tab/utf8)
```cs
using FlameCsv;

byte[] data =
    """
    id,name,age
    1,Bob,42
    2,Alice,37
    3,"Bond, James",39
    """u8.ToArray();

List<User> users = [];

// read users from utf8 bytes
foreach (var user in Csv.From(data).Read<User>())
{
    users.Add(user);
}

// write users to a stream as tab-separated fields
await Csv.To(stream).WriteAsync(
    users,
    new CsvOptions<char> { Delimiter = '\t' },
    cancellationToken);
```
---

# Dependencies

FlameCsv has two dependencies:
 - [CommunityToolkit.HighPerformance](https://github.com/CommunityToolkit/dotnet) which provides utilities for writing high-performance code, and the string pool used for header values.
- A development-time dependency to [FastExpressionCompiler](https://github.com/dadhi/FastExpressionCompiler) is used for extremely performant runtime code-generation in reflection-based code paths.
