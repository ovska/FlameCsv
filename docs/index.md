---
_layout: landing
---

# Introduction

🔥 FlameCsv 🔥 is a fully-featured high performance CSV library for .NET with a simple API, deep customization options, and built-in support for UTF8, nativeAOT, and more.

FlameCsv can be thought to be the System.Text.Json of CSV libraries. It is designed to be fast, easy to use, and extensible, while supporting low-level operations for advanced use-cases. FlameCsv is extremely fast and the most memory-efficient .NET CSV library in the world.

FlameCsv can process CSV at [tens of millions of records per second](docs/benchmarks.md#sum-the-value-of-one-column) on consumer hardware, and write arbitrarily large amounts of CSV with [near-zero allocations](docs/benchmarks.md#write-objects) irrespective of the dataset's size.

The library has thousands of unit tests and has been fuzz-tested.

See @"getting-started", view the @"examples", or deep dive into the @"FlameCsv?text=API Reference".

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
  - Parallel APIs to read/write records unordered with multiple threads
  - Low-level APIs to handle raw CSV field spans directly
- **Deep Customization**
  - Attribute configuration for header names, constructors, field order, etc.
  - Support for custom converters and converter factories (like System.Text.Json)
  - Read or write multiple CSV documents from/to a single data stream
- **Source Generators**
  - Library is fully annotated for NativeAOT and trimming
  - Source generated type maps for reflection-free reading and writing
  - Source generated enum converters with up to 10x better performance than Enum.TryParse/TryFormat

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
