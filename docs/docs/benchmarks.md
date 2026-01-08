---
uid: benchmarks
---

# Benchmarks

This page contains benchmarks comparing the performance of FlameCsv with other popular CSV libraries.

The benchmarks use varied sample CSV datasets which can be downloaded from the repository. There are both x86 (AVX-2) and ARM64 (Neon) benchmarks. x86 AVX-512 will be added if I can get my hands on a suitable CPU.

> TODO: link to commit where datasets are stored and benchmark code was run
>
> TODO: link to the raw benchmark results

## Details

- Memory allocations are on the right hand side of the bars in the charts.
- "Reflection" and "SourceGen" refer to FlameCsv's type binding method.
- "MT" means the benchmark uses unordered multithreading (parallel) APIs.
- Some libraries lack async support for certain operations; these are absent in the charts.

## Synchronous

### Read objects

This benchmark enumerates 20,000 records (10 fields per record) from a stream wrapping a `byte[]`.

Sep and RecordParser don't support automatic type binding. "Hardcoded" benchmarks have compile-time field indexes when reading.

# [Apple M4 Max 16c](#tab/arm)

<img src="../data/charts/arm/reading_objects_from_csv_sync_light.svg" alt="Read objects benchmark (sync)" class="chart-light" />
<img src="../data/charts/arm/reading_objects_from_csv_sync_dark.svg" alt="Read objects benchmark (sync)" class="chart-dark" />

# [AMD Ryzen 7 3700X](#tab/x86)

<img src="../data/charts/x86/reading_objects_from_csv_sync_light.svg" alt="Read objects benchmark (sync)" class="chart-light" />
<img src="../data/charts/x86/reading_objects_from_csv_sync_dark.svg" alt="Read objects benchmark (sync)" class="chart-dark" />

---

### Write objects

This benchmark writes the same 20,000 records from an array into a stream.

Sep and RecordParser write objects manually, other libraries supporrt automatic type binding.

# [Apple M4 Max 16c](#tab/arm)

<img src="../data/charts/arm/writing_objects_to_csv_sync_light.svg" alt="Write objects benchmark (sync)" class="chart-light" />
<img src="../data/charts/arm/writing_objects_to_csv_sync_dark.svg" alt="Write objects benchmark (sync)" class="chart-dark" />


# [AMD Ryzen 7 3700X](#tab/x86)

<img src="../data/charts/x86/writing_objects_to_csv_sync_light.svg" alt="Write objects benchmark (sync)" class="chart-light" />
<img src="../data/charts/x86/writing_objects_to_csv_sync_dark.svg" alt="Write objects benchmark (sync)" class="chart-dark" />

---

### Enumerate all fields (unquoted)

This benchmark accesses all 14 fields from a 65,536 record CSV file, read from a stream wrapping a `byte[]`.

Libraries that support accessing fields as a span into the underlying buffers are configured to do so.

# [Apple M4 Max 16c](#tab/arm)

<img src="../data/charts/arm/enumerating_csv_fields_unquoted_sync_light.svg" alt="Enumerate fields benchmark (unquoted, sync)" class="chart-light" />
<img src="../data/charts/arm/enumerating_csv_fields_unquoted_sync_dark.svg" alt="Enumerate fields benchmark (unquoted, sync)" class="chart-dark" />

# [AMD Ryzen 7 3700X](#tab/x86)

<img src="../data/charts/x86/enumerating_csv_fields_unquoted_sync_light.svg" alt="Enumerate fields benchmark (unquoted, sync)" class="chart-light" />
<img src="../data/charts/x86/enumerating_csv_fields_unquoted_sync_dark.svg" alt="Enumerate fields benchmark (unquoted, sync)" class="chart-dark" />

---

### Enumerate all fields (quoted)

This benchmark accesses all 12 fields from a 100,000 record CSV file, read from a stream wrapping a `byte[]`.

Some of the fields require unescaping. Libraries that support accessing fields as a span into the underlying buffers are configured to do so.

# [Apple M4 Max 16c](#tab/arm)

<img src="../data/charts/arm/enumerating_csv_fields_quoted_sync_light.svg" alt="Enumerate fields benchmark (quoted, sync)" class="chart-light" />
<img src="../data/charts/arm/enumerating_csv_fields_quoted_sync_dark.svg" alt="Enumerate fields benchmark (quoted, sync)" class="chart-dark" />

# [AMD Ryzen 7 3700X](#tab/x86)

<img src="../data/charts/x86/enumerating_csv_fields_quoted_sync_light.svg" alt="Enumerate fields benchmark (quoted, sync)" class="chart-light" />
<img src="../data/charts/x86/enumerating_csv_fields_quoted_sync_dark.svg" alt="Enumerate fields benchmark (quoted, sync)" class="chart-dark" />

---

### Sum the value of one column

This benchmark parses a double from a single column of the 65,536 record dataset and sums the results.
csFastFloat is used to parse the values to highlight the differences between CSV libraries better than double.Parse.

The raw `byte[]` is passed to the libraries whenever possible. There is no async variant for this benchmark.

# [Apple M4 Max 16c](#tab/arm)

<img src="../data/charts/arm/sum_the_value_of_one_column__light.svg" alt="Sum column benchmark" class="chart-light" />
<img src="../data/charts/arm/sum_the_value_of_one_column__dark.svg" alt="Sum column benchmark" class="chart-dark" />

# [AMD Ryzen 7 3700X](#tab/x86)

<img src="../data/charts/x86/sum_the_value_of_one_column__light.svg" alt="Sum column benchmark" class="chart-light" />
<img src="../data/charts/x86/sum_the_value_of_one_column__dark.svg" alt="Sum column benchmark" class="chart-dark" />

---

## Asynchronous

RecordParser doesn't support async, so it is absent from these results.

### Read objects

Sep doesn't support async parallel reading.

# [Apple M4 Max 16c](#tab/arm)

<img src="../data/charts/arm/reading_objects_from_csv_async_light.svg" alt="Read objects benchmark (async)" class="chart-light" />
<img src="../data/charts/arm/reading_objects_from_csv_async_dark.svg" alt="Read objects benchmark (async)" class="chart-dark" />

# [AMD Ryzen 7 3700X](#tab/x86)

<img src="../data/charts/x86/reading_objects_from_csv_async_light.svg" alt="Read objects benchmark (async)" class="chart-light" />
<img src="../data/charts/x86/reading_objects_from_csv_async_dark.svg" alt="Read objects benchmark (async)" class="chart-dark" />

---

### Write objects

The async version of this benchmark uses a stream that always yields on async calls, to simulate real-world async I/O.

# [Apple M4 Max 16c](#tab/arm)

<img src="../data/charts/arm/writing_objects_to_csv_async_light.svg" alt="Write objects benchmark (async)" class="chart-light" />
<img src="../data/charts/arm/writing_objects_to_csv_async_dark.svg" alt="Write objects benchmark (async)" class="chart-dark" />

# [AMD Ryzen 7 3700X](#tab/x86)

<img src="../data/charts/x86/writing_objects_to_csv_async_light.svg" alt="Write objects benchmark (async)" class="chart-light" />
<img src="../data/charts/x86/writing_objects_to_csv_async_dark.svg" alt="Write objects benchmark (async)" class="chart-dark" />

---

### Enumerate all fields (unquoted)

# [Apple M4 Max 16c](#tab/arm)

<img src="../data/charts/arm/enumerating_csv_fields_unquoted_async_light.svg" alt="Enumerate fields benchmark (unquoted, async)" class="chart-light" />
<img src="../data/charts/arm/enumerating_csv_fields_unquoted_async_dark.svg" alt="Enumerate fields benchmark (unquoted, async)" class="chart-dark" />

# [AMD Ryzen 7 3700X](#tab/x86)

<img src="../data/charts/x86/enumerating_csv_fields_unquoted_async_light.svg" alt="Enumerate fields benchmark (unquoted, async)" class="chart-light" />
<img src="../data/charts/x86/enumerating_csv_fields_unquoted_async_dark.svg" alt="Enumerate fields benchmark (unquoted, async)" class="chart-dark" />

---

### Enumerate all fields (quoted)

# [Apple M4 Max 16c](#tab/arm)

<img src="../data/charts/arm/enumerating_csv_fields_quoted_async_light.svg" alt="Enumerate fields benchmark (quoted, async)" class="chart-light" />
<img src="../data/charts/arm/enumerating_csv_fields_quoted_async_dark.svg" alt="Enumerate fields benchmark (quoted, async)" class="chart-dark" />


# [AMD Ryzen 7 3700X](#tab/x86)

<img src="../data/charts/x86/enumerating_csv_fields_quoted_async_light.svg" alt="Enumerate fields benchmark (quoted, async)" class="chart-light" />
<img src="../data/charts/x86/enumerating_csv_fields_quoted_async_dark.svg" alt="Enumerate fields benchmark (quoted, async)" class="chart-dark" />

---

## Enums

FlameCsv provides a [source generator](source-generator.md#enum-converter-generator) for enum converters that generates
highly optimized read/write operations specific to the enum. The comparisons below are performance relative to
`Enum.TryParse` or `Enum.TryFormat`.

Generating the enum converter at compile-time allows the enum to be analyzed, and specific optimizations to be made
regarding different values and names.
The generated converter especially excels at small enums that start from 0 without any gaps, and have only ASCII
characters in their name. More complex configurations such as flags and non-ASCII display names are supported as well.

The benchmarks below are for the `System.TypeCode`-enum, either in UTF8 (`byte`) or UTF16 (`char`).
You can find the generated code for the enum under [Source Generator](source-generator.md#enum-converter-generator).
Benchmarked on AMD Ryzen 7 3700X.

### Parsing

The chart shows relative throughput of parsing enums using the reflection-based converter in FlameCsv,
and the source-generated converter (`Enum.TryParse` is the baseline at 100%). Higher is better.

<img src="../data/charts/parse_light.svg" alt="Enum parsing performance chart" class="chart-light" />
<img src="../data/charts/parse_dark.svg" alt="Enum parsing performance chart" class="chart-dark" />

<details>
<summary><strong>Click to view benchmark summary</strong></summary>

| Parameter | Description |
| ---------- | ------------ |
| Bytes | Parsing from UTF8 (`byte`) or UTF16 (`char`) |
| IgnoreCase | Parsing is case-insensitive |
| ParseNumbers | Input is numeric and not enum names |

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

</details>

### Formatting

The chart shows relative throughput of formatting enums using the reflection-based converter in FlameCsv,
and the source-generated converter (`Enum.TryFormat` is the baseline at 100%). Higher is better.

<img src="../data/charts/format_light.svg" alt="Enum formatting performance chart" class="chart-light" />
<img src="../data/charts/format_dark.svg" alt="Enum formatting performance chart" class="chart-dark" />

<details>
<summary><strong>Click to view benchmark summary</strong></summary>

The table shows results for formatting directly using `Enum.TryFormat`, formatting using the reflection-based
converter in FlameCsv, and the source-generated converter.

| Parameter | Description |
| ---------- | ------------ |
| Numeric | Formatting as numbers and not enum names |
| Bytes | Formatting to UTF8 (`byte`) or UTF16 (`char`) |

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

</details>

## Why not NCsvPerf

While NCsvPerf benchmarks are commonly used for CSV library comparisons, it has several limitations:

1. String Conversion: All fields are converted to strings, which:
   - Creates unnecessary transcoding and GC overhead
   - Doesn't reflect modern libraries' ability to work with memory spans directly
2. List Accumulation: Records are collected into a list before returning, which adds unnecessary overhead, and is not representative of how large datasets would be consumed
3. Data Homogeneity: The test data lacks real-world complexity like quoted and escaped fields

NCsvPerf doesn't really stress the capabilities of modern CSV libraries effectively; with the speed of modern CSV libraries it's mostly a test of "how many strings can the .NET runtime create in a second".
