# 🔥 FlameCsv 🔥

FlameCsv is the System.Text.Json of CSV libraries. It is designed to be fast, easy to use, and extensible, while supporting low-level operations for advances use-cases. It is consistently the fastest .NET CSV library in reading, writing, and enumerating CSV data, and does this while also allocating the least memory.

Includes built-in support for UTF8, nativeAOT, and more. Read CSV at multiple gigabytes per second on consumer hardware,
and write arbitrarily large amounts of CSV with near-zero allocations.
FlameCsv leverages modern .NET patterns such as spans, SIMD hardware intrinsics, memory/string pooling, and buffer writers, and is built from the ground up to provide an easy-to-use high performance experience.

The library has thousands of tests, and critical paths have been fuzz-tested with SharpFuzz.

See the [documentation](https://ovska.github.io/FlameCsv) for more information and examples.

# Features

- 💡 **Ease of Use**
    - Simple API for reading and writing CSV
    - Built-in support for common CLR types
    - Supports both synchronous and asynchronous operations
    - Flexible; read or write almost any data source
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
    - Enum converter generator for up to 10x faster enum parsing and 7x faster formatting

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

# Changelog

## 0.4.0
- *Breaking:* Only ASCII tokens are now supported in the dialect (delimiter, quote, newline, etc.)
- *Breaking:* Refactored the internal streaming I/O for performance improvements in real-world scenarios.
- *Breaking:* Replaced `Whitespace` with `Trimming` for simplicity and consistency with other libraries.
- *Breaking:* `CsvFieldQuoting` is now a flags enum, and zero values is `CsvFieldQuoting.Never`. Library default has not changed.
- *Breaking:* Argument to the exception handler no longer a `ref struct` or has `in`-modifier
- *Breaking:* ImmutableArray used instead of ReadOnlySpan for header parsing (may require recompile for source generated files)
- *Breaking:* Delete `CsvAsyncWriter<T>` (combined with `CsvWriter<T>`), rename `ColumnIndex` to `FieldIndex`, moved configuration to instance instead of factory method
- *Breaking*: `CsvOptions<T>` is now sealed
- *Breaking*: Removed `in` and `ref struct` modifier from exception handler
- *Breaking:* Renamed `CsvRecord` -> `CsvPreservedRecord` and `CsvValueRecord` -> `CsvRecord`
- *Breaking:* Calling `Complete` or `CompleteAsync` on `ICsvBufferWriter<T>` no longer throws the passed exception
- Added support for headerless CSV with the source generator
- Added support for `ISpanParsable<T>` and `ISpanFormattable` to the source generator
- Added explicit UTF8 reader and writer types for up to 2x faster I/O compared to TextReader/TextWriter when using ASCII or UTF8 encoding
- Added hot reload support for enum changes
- Parsing performance improvements for all architectures (especially Avx512BW when reading `char`)
- Removed explicit dependency to `System.IO.Pipelines` package
- Fixed unreachable code warnings in some source generated enum byte converters
- Fixed potential parsing error when reading broken data on ARM64 platforms
- Fixed potential layout bugs on certain architestures when caching type materializers with headers
- Increased default buffer sizes from 4K to 16K (and 32K for file I/O)

## 0.3.1

- Fixed streaming readers allocating memory too often

## 0.3.0

- *Breaking:* Newline parsing is more lenient (e.g. `\r\n` can parse `\n` and `\r` as well), newline no longer nullable
- *Breaking:* Surrogate `char`s not supported in dialect
- Added support for flags enums in both reflection and source generated converters
- Added AVX-512 support for parsing
- Improved unescaping performance by up to 50%
- Added fuzz testing projects

## 0.2.0

- *Breaking:* Removed `Async`-suffix from `CsvReader` methods that support synchronous enumeration
- *Breaking:* Removed `CsvUnhandledException`, value enumerators now throw without wrapping the exception
- Added support for `EnumNameAttribute`
- Added enum converter source generator
- Added UTF-8 BOM skipping
- Fixed `char` converter inconsistencies on non-ASCII inputs
- Removed unnecessary SIMD instructions when parsing chars

## 0.1.0

Initial release.
