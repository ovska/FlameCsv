---
uid: benchmarks
---

# Benchmarks

Not all benchmarks test all libraries; if a library doesn't provide built-in data binding, it is
not benchmarked for reading full records as .NET objects.

The benchmarks below are done with the following setup (sadly no AVX512 compatible CPU available),
using the default configuration in BenchmarkDotNet v0.14.0 (unless otherwise stated).
```
BenchmarkDotNet v0.14.0, Windows 10 (10.0.19045.5608/22H2/2022Update)
AMD Ryzen 7 3700X, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.100
  [Host]     : .NET 9.0.0 (9.0.24.52809), X64 RyuJIT AVX2
  DefaultJob : .NET 9.0.0 (9.0.24.52809), X64 RyuJIT AVX2
```

The benchmarks use commonly used CSV datasets.

## Results

> TODO: async tests

### Reading .NET objects

The dataset is 5000 records of 10 fields of varied data, quoted fields, and escaped quotes.
The data is read from a pre-loaded byte array.

| Method                | Mean     |  Ratio | Allocated | Alloc Ratio |
|----------------------:|---------:|-------:|----------:|------------:|
| FlameCsv (Reflection) | 2.308 ms |   1.00 |   1.66 MB |        1.00 |
| FlameCsv (SourceGen)  | 2.506 ms |   1.09 |   1.66 MB |        1.00 |
| Sylvan                | 2.570 ms |   1.11 |   2.64 MB |        1.59 |
| RecordParser          | 4.673 ms |   2.02 |   1.93 MB |        1.16 |
| CsvHelper             | 6.424 ms |   2.78 |   3.49 MB |        2.10 |

> TODO: link to specific commit and dataset

### Reading without processing all fields

The dataset is 65535 records of 14 fields (no quotes or escapes). The benchmark calculates the sum of a single numerical field.
The data is read from a pre-loaded byte array.

| Method        | Mean      | Ratio | Allocated | Alloc Ratio |
|--------------:|----------:|------:|----------:|------------:|
| FlameCsv      |  3.292 ms |  1.00 |     322 B |        1.00 |
| Sep           |  4.431 ms |  1.35 |    5942 B |       18.45 |
| Sylvan        |  5.014 ms |  1.52 |   42029 B |      130.52 |
| RecordParser  |  6.358 ms |  1.93 | 2584418 B |    8,026.14 |
| CsvHelper     | 34.877 ms | 10.60 | 2789195 B |    8,662.10 |

> TODO: link to specific commit and dataset

### Writing .NET objects

The same dataset of 5000 records as above is written to @"System.IO.TextWriter.Null?displayProperty=nameWithType".
The objects are pre-loaded to an array.

| Method                | Mean     | Ratio | Allocated | Alloc Ratio |
|----------------------:|---------:|------:|----------:|------------:|
| FlameCsv (SourceGen)  | 3.196 ms |  1.00 |     170 B |        1.00 |
| FlameCsv (Reflection) | 3.302 ms |  1.03 |     174 B |        1.02 |
| Sylvan                | 3.467 ms |  1.08 |   33605 B |      197.68 |
| Sep                   | 3.561 ms |  1.11 |  121181 B |      712.83 |
| CsvHelper             | 7.806 ms |  2.44 | 2077347 B |   12,219.69 |
| RecordParser          | 9.245 ms |  2.89 | 8691788 B |   51,128.16 |

> TODO: link to specific commit and dataset

## Cold-start

> TODO: implement cold-start benchmarks

## About performance

Performance has been a key consideration for FlameCsv since the beginning. This means:
- Maximum CPU utilization through SIMD hardware intrinsics
- Minimal data copying
- Minimal allocations
- Performance parity between synchronous and asynchronous operations

Performance isn't just about records processed per second. Allocations and garbage collection can significantly impact workloads, especially in highly parallel scenarios like web servers. Similarly, streaming capabilities are crucial when reading large files, particularly in server environments.

When writing CSV data, performance is primarily bottlenecked by:
- Data copying I/O
- UTF16-UTF8 transcoding
- Formatting numbers and other formattable types

### Throughput

The most basic performance metric: how quickly does the library process data.

Raw reading throughput benchmarks use pre-allocated data (e.g., an array) to minimize code execution outside the measured operations. We benchmark:
- Parsing raw CSV records/fields
- Different methods of reading records as .NET types

Asynchronous reading of pre-allocated data can reveal performance overhead in async implementations, which is surprisingly large in some CSV libraries.

Benchmarking writes using no-op destinations like @"System.IO.Stream.Null?displayProperty=nameWithType" or @"System.IO.TextWriter.Null?displayProperty=nameWithType" helps measure the library's overhead, though real-world performance may vary due to buffer size differences (which are typically configurable).


### Memory Usage

Lower object allocation means less garbage collector overhead. This is particularly important in web servers handling concurrent operations.
Memory usage is best evaluated by _comparing_ libraries, since some operations (like reading strings) inherently require allocations,
so looking at the allocation numbers in isolation may not be useful.

Streaming is another crucial factor. While important for servers, it's essential for handling large files that cannot fit in memory.
This includes I/O and `I(Async)Enumerable`. A well-implemented streaming library can handle workloads of any size without issues
by reading and writing without having to buffer the entire dataset in memory.


### Cold start vs. long-running

BenchmarkDotNet typically runs code multiple times to eliminate startup overhead, JIT compilation, and other variables.
FlameCsv benchmarks follow this approach unless specified otherwise.

However, cold start performance matters more for:
- Serverless applications (Azure Functions / AWS Lambda)
- Desktop/CLI applications performing one-off operations

Reflection-based code (like compiled expression delegates) typically performs poorly on cold starts compared to handwritten or source-generated code,
though these differences diminish in long-running operations.


### Why not NCsvPerf

While NCsvPerf is commonly used for CSV library comparisons, it has several limitations:

1. String Conversion: All fields are converted to strings, which:
   - Creates unnecessary overhead
   - Doesn't reflect modern libraries' ability to work with memory spans
   - Significantly impacts CPU and memory measurements

2. List Accumulation: Records are collected into a list before returning, which:
   - Adds unnecessary CPU and memory overhead
   - Doesn't reflect streaming capabilities which are critical in reading large files

3. Data Homogeneity: The test data lacks real-world complexity like:
   - Quoted values
   - Escaped characters
   - Mixed data types

While NCsvPerf provides some insights into CSV parsing performance, it's not ideal for comprehensive real-world comparisons.

