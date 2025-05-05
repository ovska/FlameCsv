---
uid: benchmarks
---

# Benchmarks

This page contains benchmarks comparing the performance of FlameCsv with other popular CSV libraries.
If a library doesn't provide built-in data binding, it is not benchmarked for reading full records as .NET objects.

The benchmarks below are done with the following setup
using the default configuration in BenchmarkDotNet v0.14.0 (unless otherwise stated).
Benchmarks for 0.3.0 with AVX-512 support are coming soon.
```
BenchmarkDotNet v0.14.0, Windows 10 (10.0.19045.5608/22H2/2022Update)
AMD Ryzen 7 3700X, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.100
  [Host]     : .NET 9.0.0 (9.0.24.52809), X64 RyuJIT AVX2
  DefaultJob : .NET 9.0.0 (9.0.24.52809), X64 RyuJIT AVX2
```

The benchmarks use commonly used CSV datasets, and can be downloaded from the repository. You can browse the code and datasets
[here](https://github.com/ovska/FlameCsv/tree/96e4d5b6317495f1f2c8c6e525b8e817738872a2/FlameCsv.Benchmark/Comparisons).

## Results

### Reading .NET objects

The dataset is 5000 records of 10 fields of varied data, quoted fields, and escaped quotes.
The data is read from a pre-loaded byte array to simulate real-world scenarios. FlameCSV is even faster comparatively
when reading from a string.

| Method                | Mean     |  Ratio | Allocated | Alloc Ratio |
|----------------------:|---------:|-------:|----------:|------------:|
| FlameCsv (Reflection) | 2.308 ms |   1.00 |   1.66 MB |        1.00 |
| FlameCsv (SourceGen)  | 2.506 ms |   1.09 |   1.66 MB |        1.00 |
| Sylvan                | 2.570 ms |   1.11 |   2.64 MB |        1.59 |
| RecordParser          | 4.673 ms |   2.02 |   1.93 MB |        1.16 |
| CsvHelper             | 6.424 ms |   2.78 |   3.49 MB |        2.10 |

<img src="../data/charts/read_light.svg" alt="Reading 5000 records into .NET objects" class="chart-light" />
<img src="../data/charts/read_dark.svg" alt="Reading 5000 records into .NET objects" class="chart-dark" />

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

<img src="../data/charts/peek_light.svg" alt="Computing sum of one field from 65535 records" class="chart-light" />
<img src="../data/charts/peek_dark.svg" alt="Computing sum of one field from 65535 records" class="chart-dark" />

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

<img src="../data/charts/write_light.svg" alt="Writing 5000 records" class="chart-light" />
<img src="../data/charts/write_dark.svg" alt="Writing 5000 records" class="chart-dark" />

> Note that the Y axis for "Mean" doesn't start from 0 in this chart (this is Excel's default behavior for this dataset)

### Cold-start

> TODO: implement cold-start benchmarks

### Async

Here are the reading benchmarks using async overloads (where available). The test setup is same as before (no actual IO is done),
is meant to demonstrate the overhead of async versions.

Writing benchmarks are not included as they are expected to not be significantly different from the synchronous versions
(IO should only happen when flushing).

#### Reading .NET objects

| Method                | Mean     | Ratio | Allocated | Alloc Ratio |
|----------------------:|---------:|------:|----------:|------------:|
| FlameCsv (Reflection) | 2.307 ms |  1.00 |   1.66 MB |        1.00 |
| FlameCsv (SourceGen)  | 2.408 ms |  1.04 |   1.66 MB |        1.00 |
| Sylvan                | 2.891 ms |  1.25 |   2.65 MB |        1.59 |
| CsvHelper             | 6.672 ms |  2.89 |    3.5 MB |        2.11 |

#### Reading without processing all fields

| Method     | Mean      | Ratio | Allocated | Alloc Ratio |
|-----------:|----------:|------:|----------:|------------:|
| FlameCsv   |  3.771 ms |  1.00 |     632 B |        1.00 |
| Sep        |  4.764 ms |  1.26 |    5944 B |        9.41 |
| Sylvan     |  6.408 ms |  1.70 |   78102 B |      123.58 |
| CsvHelper  | 36.902 ms |  9.79 | 2935048 B |    4,644.06 |


### Enums

FlameCsv provides a [source generator](source-generator.md#enum-converter-generator) for enum converters that generates
highly optimized read/write operations specific to the enum. The comparisons below are performance relative to
`Enum.TryParse` or `Enum.TryFormat`.

Generating the enum converter at compile-time allows the enum to be analyzed, and specific optimizations to be made
regarding different values and names.
The generated converter especially excels at small enums that start from 0 without any gaps, and have only ASCII
characters in their name. More esoteric configurations such as emojis as display names are supported as well.

The benchmarks below are for handling the @"System.TypeCode"-enum, either in `byte` or `char` (the Bytes-column).

#### Parsing

<img src="../data/charts/parse_light.svg" alt="Enum parsing performance chart" class="chart-light" />
<img src="../data/charts/parse_dark.svg" alt="Enum parsing performance chart" class="chart-dark" />

#### Formatting

<img src="../data/charts/format_light.svg" alt="Enum formatting performance chart" class="chart-light" />
<img src="../data/charts/format_dark.svg" alt="Enum formatting performance chart" class="chart-dark" />

#### Exact results

| Method     | Bytes | IgnoreCase | ParseNumbers | Mean      | StdDev    | Ratio |
|----------- |------ |----------- |------------- |----------:|----------:|------:|
| TryParse   | False | False      | False        | 582.33 ns |  3.088 ns |  1.00 |
| Reflection | False | False      | False        | 300.89 ns |  0.340 ns |  0.52 |
| SourceGen  | False | False      | False        |  79.76 ns |  1.273 ns |  0.14 |
|            |       |            |              |           |           |       |
| TryParse   | False | False      | True         | 185.49 ns |  2.101 ns |  1.00 |
| Reflection | False | False      | True         | 304.56 ns |  2.484 ns |  1.64 |
| SourceGen  | False | False      | True         |  78.30 ns |  0.701 ns |  0.42 |
|            |       |            |              |           |           |       |
| TryParse   | False | True       | False        | 661.59 ns |  6.298 ns |  1.00 |
| Reflection | False | True       | False        | 369.34 ns |  3.516 ns |  0.56 |
| SourceGen  | False | True       | False        |  82.75 ns |  1.265 ns |  0.13 |
|            |       |            |              |           |           |       |
| TryParse   | False | True       | True         | 186.26 ns |  1.584 ns |  1.00 |
| Reflection | False | True       | True         | 368.88 ns |  3.205 ns |  1.98 |
| SourceGen  | False | True       | True         |  83.87 ns |  1.198 ns |  0.45 |
|            |       |            |              |           |           |       |
| TryParse   | True  | False      | False        | 726.99 ns | 15.936 ns |  1.00 |
| Reflection | True  | False      | False        | 480.53 ns |  0.941 ns |  0.66 |
| SourceGen  | True  | False      | False        |  73.65 ns |  0.433 ns |  0.10 |
|            |       |            |              |           |           |       |
| TryParse   | True  | False      | True         | 326.83 ns |  0.540 ns |  1.00 |
| Reflection | True  | False      | True         | 485.12 ns |  4.999 ns |  1.48 |
| SourceGen  | True  | False      | True         |  72.26 ns |  0.196 ns |  0.22 |
|            |       |            |              |           |           |       |
| TryParse   | True  | True       | False        | 785.22 ns |  1.791 ns |  1.00 |
| Reflection | True  | True       | False        | 574.11 ns |  6.201 ns |  0.73 |
| SourceGen  | True  | True       | False        |  72.89 ns |  0.869 ns |  0.09 |
|            |       |            |              |           |           |       |
| TryParse   | True  | True       | True         | 327.22 ns |  3.023 ns |  1.00 |
| Reflection | True  | True       | True         | 560.96 ns |  5.796 ns |  1.71 |
| SourceGen  | True  | True       | True         |  71.82 ns |  0.928 ns |  0.22 |

| Method     | Numeric | Bytes | Mean       | StdDev  | Ratio |
|----------- |-------- |------ |-----------:|--------:|------:|
| TryFormat  | False   | False |   715.8 ns | 1.73 ns |  1.00 |
| Reflection | False   | False |   275.2 ns | 1.63 ns |  0.38 |
| SourceGen  | False   | False |   188.4 ns | 0.27 ns |  0.26 |
|            |         |       |            |         |       |
| TryFormat  | False   | True  | 1,296.3 ns | 1.33 ns |  1.00 |
| Reflection | False   | True  |   285.8 ns | 0.24 ns |  0.22 |
| SourceGen  | False   | True  |   173.6 ns | 0.14 ns |  0.13 |
|            |         |       |            |         |       |
| TryFormat  | True    | False |   285.0 ns | 0.64 ns |  1.00 |
| Reflection | True    | False |   298.5 ns | 0.24 ns |  1.05 |
| SourceGen  | True    | False |   151.6 ns | 0.43 ns |  0.53 |
|            |         |       |            |         |       |
| TryFormat  | True    | True  |   861.6 ns | 0.81 ns |  1.00 |
| Reflection | True    | True  |   298.9 ns | 0.45 ns |  0.35 |
| SourceGen  | True    | True  |   156.2 ns | 2.35 ns |  0.18 |

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

Fewer allocations result in less garbage collector overhead. This is particularly important in web servers handling concurrent operations.
Memory usage is best evaluated by _comparing_ libraries, since some operations (like creating strings) inherently require allocations,
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

While the NCsvPerf benchmarks are commonly used for CSV library comparisons, it has several limitations:

1. String Conversion: All fields are converted to strings, which:
   - Creates unnecessary transcoding overhead
   - Stresses the garbage collector needlessly
   - Doesn't reflect modern libraries' ability to work with memory spans directly
   - Significantly impacts CPU and memory measurements

2. List Accumulation: Records are collected into a list before returning, which:
   - Adds unnecessary CPU and memory overhead
   - Doesn't reflect streaming capabilities which are critical when reading large files

3. Data Homogeneity: The test data lacks real-world complexity like:
   - Quoted values
   - Escaped characters
   - Mixed data types

While NCsvPerf provides some insights into CSV parsing performance, it's not ideal for comprehensive real-world comparisons.

