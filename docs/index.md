---
_layout: landing
---

# Introduction

🔥 FlameCSV 🔥 is a high-performance CSV parsing and formatting library for .NET with low barrier of entry, deep customization options, and built-in support for UTF8, nativeAOT, and more.

See @"getting-started", view the @"examples", or deep dive into the @"FlameCsv?text=API Reference".

# Features

- 💡 **Ease of Use**
  - Simple API for reading and writing CSV
  - Built-in support for common CLR types
  - Supports both synchronous and asynchronous operations
  - Flexible; read or write almost any data source
  - Automatic newline detection
  - UTF-8/ASCII support directly to/from bytes without additional transcoding
- 🚀 **High Performance**
  - Optimized for speed and low memory usage
  - SIMD-accelerated parsing routines with hardware intrinsics
  - Batteries-included internal caching and memory pooling for near-zero allocations
  - Reflection code paths that rival manually written code in performance
- 🛠️ **Deep Customization Options**
  - Read or write either .NET objects, or raw CSV records and fields
  - Attribute configuration for header names, constructors, field order, and more
  - Support for custom converters and converter factories
  - Read or write multiple CSV documents from/to a single data stream
- ✍️ **Source Generator**
  - Fully compatible with NativeAOT
  - Supports trimming to reduce application size
  - View and debug the code instead of opaque reflection
  - Compile-time diagnostics instead of runtime errors

# Example

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
foreach (var user in CsvReader.Read<User>(data))
{
    users.Add(user);
}

// write users to a stream as as tab-separated utf8
await CsvWriter.WriteAsync(
    stream,
    users,
    new CsvOptions<byte> { Delimiter = '\t' },
    cancellationToken);
```

# Dependencies

FlameCsv has two dependencies:
 - The excellent [FastExpressionCompiler](https://github.com/dadhi/FastExpressionCompiler) libary that is used for runtime code generation in non-sourcegen scenarios.
 - [System.IO.Pipelines](https://www.nuget.org/packages/system.io.pipelines/) for the powerful abstractions and additional @"FlameCsv.CsvReader.ReadAsync``1(System.IO.Pipelines.PipeReader,FlameCsv.Binding.CsvTypeMap{System.Byte,``0},FlameCsv.CsvOptions{System.Byte})?text=reading" and @"FlameCsv.CsvWriter.WriteAsync``1(System.IO.Pipelines.PipeWriter,System.Collections.Generic.IAsyncEnumerable{``0},FlameCsv.Binding.CsvTypeMap{System.Byte,``0},FlameCsv.CsvOptions{System.Byte},System.Threading.CancellationToken)?text=writing" APIs

It's possible in the future to split to separate packages to keep the core library dependency-free, but there are no hard plans for it yet.
