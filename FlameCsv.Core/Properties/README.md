# 🔥 FlameCSV 🔥

Fully-featured high performance CSV library for .NET with a simple API, deep customization options,
and built-in support for UTF8, nativeAOT, and more. Read CSV at multiple gigabytes per second on consumer hardware,
and write arbitrarily large amounts of CSV with near-zero allocations. FlameCSV leverages modern .NET patterns and
libraries such as spans, SIMD hardware intrinsics, memory/string pooling, pipes and buffer writers, and
is built from the ground up to provide an easy-to-use high performance experience.

# Features

- 💡 **Ease of Use**
    - Simple API for reading and writing CSV
    - Built-in support for common CLR types
    - Supports both synchronous and asynchronous operations
    - Flexible; read or write almost any data source
    - Automatic newline detection
    - UTF-8/ASCII support to read/write bytes directly without additional transcoding
    - Supports hot reload
- 🚀 **High Performance**
    - Optimized for speed and low memory usage
    - Specialized SIMD-accelerated parsing and unescaping
    - Batteries-included internal caching and memory pooling for near-zero allocations
    - Reflection code paths that rival and exceed manually written code in performance
- 🛠️ **Deep Customization**
    - Read or write either .NET objects, or raw CSV records and fields
    - Attribute configuration for header names, constructors, field order, and more
    - Support for custom converters and converter factories
    - Read or write multiple CSV documents from/to a single data stream
- ✍️ **Source Generator**
    - Fully annotated and compatible with NativeAOT
    - Supports trimming to reduce application size
    - Debuggable source code instead of compiled expressions
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

// read objects from the string
foreach (var user in CsvReader.Read<User>(data))
{
    users.Add(user);
}

// write to stream using a different delimiter
await CsvWriter.WriteAsync(
    new StreamWriter(Stream.Null, Encoding.UTF8),
    users,
    new CsvOptions<char> { Delimiter = '\t' },
    cancellationToken);
```
