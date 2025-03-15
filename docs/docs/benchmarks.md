---
uid: benchmarks
---

# About performance

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

## Throughput

The most basic performance metric: how quickly does the library process data.

Raw reading throughput benchmarks use pre-allocated data (e.g., an array) to minimize code execution outside the measured operations. We benchmark:
- Parsing raw CSV records/fields
- Different methods of reading records as .NET types

Asynchronous reading of pre-allocated data can reveal performance overhead in async implementations, which is surprisingly large in some CSV libraries.

Benchmarking writes using no-op destinations like @"System.IO.Stream.Null?displayProperty=nameWithType" or @"System.IO.TextWriter.Null?displayProperty=nameWithType" helps measure the library's overhead, though real-world performance may vary due to buffer size differences (which are typically configurable).


## Memory Usage

Lower object allocation means less garbage collector overhead. This is particularly important in web servers handling concurrent operations.
Memory usage is best evaluated by _comparing_ libraries, since some operations (like reading strings) inherently require allocations,
so looking at the allocation numbers in isolation may not be useful.

Streaming is another crucial factor. While important for servers, it's essential for handling large files that cannot fit in memory.
This includes I/O and `I(Async)Enumerable`. A well-implemented streaming library can handle workloads of any size without issues
by reading and writing without having to buffer the entire dataset in memory.


## Cold start vs. long-running

BenchmarkDotNet typically runs code multiple times to eliminate startup overhead, JIT compilation, and other variables.
FlameCsv benchmarks follow this approach unless specified otherwise.

However, cold start performance matters more for:
- Serverless applications (Azure Functions / AWS Lambda)
- Desktop/CLI applications performing one-off operations

Reflection-based code (like compiled expression delegates) typically performs poorly on cold starts compared to handwritten or source-generated code,
though these differences diminish in long-running operations.


## Why not NCsvPerf

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
