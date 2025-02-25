---
uid: benchmarks
---

# About performance

Since the first line of code, performance was an important consideration for FlameCsv. In a nutshell, this means
minimal data copying, minimal CPU use, and minimal allocations (which doesn't always mean maximum throughput).
Another important factor was feature and performance parity of asynchronous operations.

_Performance_ isn't a black and white concept about records read per second, because allocations and garbage collection
can have a large impact depending on the workload, especially in highly parallel scenarios such as web servers.
Similarly, streaming is an important factor when reading large files, more so in servers (along with async).
Below are some performance metrics that FlameCsv was optimized for.


## Throughput

Obviously the simplest metric of performance is the amount of data processed per second. This also translates to less
CPU use for the same data.

Raw throughput is best benchmarked with pre-allocated data (e.g. an array) to have as little code running as possible
outside of the benchmarked code. Throughput is benchmarked with both parsing raw CSV records/fields, as well as the
different ways of reading records as .NET types.

Reading pre-allocated data asynchronously can also be revealing to expose some performance overhead that the async
code creates. This is especially major in some CSV libraries.


## Memory usage

The less objects a library allocates, the less it taxes the garbage collector. This is especially important in
web servers, where multiple operations can be in-flight concurrently. This can be benchmarked by simply
reading and writing CSV as would be done usually. Memory usage is best evaluated as comparisons to other libraries; e.g.,
when reading .NET types with lots of strings, there will always be a large number of allocations, so judging the results
by an absolute kb/Mb may not be useful.

Another very important factor is _streaming_. While it is also important in servers, it's paramount everywhere when reading very large files.
Some files are infeasibly big to be read into memory, and might even be too large to fit into memory completely at all.
FlameCsv supports both reading and writing both sync and async streaming data (both I/O and .NET types).
Fortunately, most libraries are pretty streaming friendly thanks to the prevalence of enumerable in .NET.

All benchmarks contain the memory usage analyzer, and there are some benchmarks dedicated to reading
buffered data (such as one would do when reading files or HTTP request bodies).


## Cold start vs. long-running

By default (and in most .NET benchmarks), BenchmarkDotNet runs the code multiple times to get rid of initial startup
times, JITting and other confounding factors. Unless otherwise stated, all FlameCsv benchmarks are like this.

However, things like Azure Functions / AWS Lambda, and desktop/CLI applications might read/write files only once, or only a few times.
In these scenarios, the cold start performance is more important than repeated runs.

Reflection-heavy code (such as compiled expression delegates) is usually pretty terrible in cold starts, but better in long running operations.
Source generation and ahead-of-time compilation is a huge boon in this metric.
There are a few cold start benchmarks measuring CPU and memory use.


## Why not NCsvPerf

While the benchmarks in NCsvPerf have been used to compare different CSV libraries, it's sadly not very well suited for the task.
Here are some problems in the reading benchmarks:

- All fields are converted to strings. This is wholly unnecessary, especially for modern libraries that
  offer copy-free slices into the existing memory using spans. This problem alone completely pollutes the memory usage portion
  of the benchmark, and dominates the CPU time as well.
- All records are added to a list before returning. This creates unnecessary additional CPU and memory overhead,
  and should not be needed since simple C# language syntax can be used to stream back objects from just about any
  library that can read one record at a time.
- The data is too homogenous. While it might be a good example of _one_ kind of data CSV libraries are used to read,
  it lacks some nuances of real world data such as quoted fields (biggest problem), different line endings, and whitespace in fields.

While I appreciate the work that has gone into it, both the implementation and data in NCsvPerf are not well suited
for comparing different libraries.
