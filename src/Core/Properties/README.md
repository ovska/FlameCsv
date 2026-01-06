# 🔥 FlameCsv 🔥

FlameCsv is the System.Text.Json of CSV libraries. It is designed to be fast, easy to use, and extensible, while supporting low-level operations for advances use-cases. It is consistently the fastest .NET CSV library in reading, writing, and enumerating CSV data, and does this while also allocating the least memory.

Includes built-in support for UTF8, nativeAOT, and more. Read CSV at multiple gigabytes per second on consumer hardware,
and write arbitrarily large amounts of CSV with near-zero allocations.
FlameCsv leverages modern .NET patterns such as spans, SIMD hardware intrinsics, memory/string pooling, and buffer writers, and is built from the ground up to provide an easy-to-use high performance experience.

The library has thousands of tests, and critical paths have been fuzz-tested with SharpFuzz.

See the [documentation](https://ovska.github.io/FlameCsv) for more information and examples.

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
foreach (var user in Csv.From(data).Read<User>())
{
    users.Add(user);
}

// write to stream using a different delimiter
await Csv
    .To(Stream.Null, Encoding.UTF8)
    .WriteAsync(
        users,
        new CsvOptions<char> { Delimiter = '\t' },
        cancellationToken);
```

# Changelog

## 1.0.0
- *Breaking:* Changed public API from `CsvReader` and `CsvWriter` to `Csv.From(...)` and `Csv.To(...)` for improved discoverability and reduced overload count explosion
- *Breaking:* Unix-style backslash escaping support removed
- *Breaking:* Only ASCII tokens are now supported in the dialect (delimiter, quote, newline, etc.)
- *Breaking:* Replaced `Whitespace` with `Trimming` for simplicity and consistency with other libraries.
- *Breaking:* `CsvFieldQuoting` is now a flags enum, and zero is `CsvFieldQuoting.Never`. Library default has not changed.
- *Breaking:* Moved `IgnoreDuplicateHeaders` to `CsvOptions<T>`. Changed for the last match to win instead of first.
- *Breaking:* Moved `IgnoreUnmatchedHeaders` to `CsvOptions<T>`.
- *Breaking:* Argument to the exception handler no longer a `ref struct` or has `in`-modifier
- *Breaking:* ImmutableArray used instead of ReadOnlySpan for header parsing (may require recompile for source generated files)
- *Breaking:* Deleted `CsvAsyncWriter<T>` (combined with `CsvWriter<T>`), renamed `ColumnIndex` to `FieldIndex`, moved configuration to instance instead of factory method
- *Breaking:* `CsvOptions<T>` is now sealed
- *Breaking:* Removed `in` and `ref struct` modifier from exception handler
- *Breaking:* Renamed `CsvRecord` -> `CsvPreservedRecord` and `CsvValueRecord` -> `CsvRecord`
- *Breaking:* Calling `Complete` or `CompleteAsync` on `ICsvBufferWriter<T>` no longer throws the passed exception
- *Breaking:* Refactored the internal streaming I/O for performance improvements in real-world scenarios. This is only an issue if you've inherited the I/O types
- *Breaking:* The record parameter to `IMaterializer<T>` is no longer `ref` (the record struct is now only 32 bytes)
- Added a huge amount of parsing performance improvements on all architectures
- Added support for NET10 and new AVX512 mask instructions
- Added support for headerless CSV with the source generator
- Added support for `ISpanParsable<T>` and `ISpanFormattable` to the source generator
- Added specialized ARM64/NEON parser
- Added explicit UTF8 reader and writer types for up to 2x faster I/O compared to TextReader/TextWriter when using ASCII or UTF8 encoding
- Added hot reload support for enum changes (if they are eventually supported by the runtime)
- Removed explicit dependency to `System.IO.Pipelines` package
- Fixed potential problems when escaping fields with quotes near the end
- Fixed unreachable code warnings in some source generated enum byte converters
- Fixed potential parsing error when reading broken data on ARM64 platforms
- Fixed potential layout bugs on certain architectures when caching type materializers with headers
- Fixed delimiters sometimes not being written with `CsvWriter<T>` when writing full records
- Increased default I/O buffer sizes from 4K to 16K (and 32K for file I/O)

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
