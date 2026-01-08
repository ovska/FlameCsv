```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.2 (25C56) [Darwin 25.2.0]
Apple M4 Max, 1 CPU, 16 logical and 16 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.0, 10.0.25.52411), Arm64 RyuJIT armv8.0-a
  Job-CASMLP : .NET 10.0.0 (10.0.0, 10.0.25.52411), Arm64 RyuJIT armv8.0-a

MinIterationTime=1s  Server=True  MaxIterationCount=48  
MaxWarmupIterationCount=32  MinIterationCount=16  MinWarmupIterationCount=16  

```
| Method                           | Async | Mean        | Error    | StdDev   | Ratio | RatioSD | Gen0    | Gen1    | Allocated | Alloc Ratio |
|--------------------------------- |------ |------------:|---------:|---------:|------:|--------:|--------:|--------:|----------:|------------:|
| **_FlameCsv**                        | **False** |  **3,206.0 μs** | **12.50 μs** | **12.28 μs** |  **1.00** |    **0.01** | **41.0156** |       **-** |   **6.61 MB** |        **1.00** |
| _Flame_SrcGen                    | False |  3,208.7 μs |  8.65 μs |  8.49 μs |  1.00 |    0.00 | 41.0156 |       - |   6.61 MB |        1.00 |
| _FlameCsv_Reflection_Parallel    | False |  1,155.5 μs |  2.24 μs |  1.99 μs |  0.36 |    0.00 | 43.9453 | 11.7188 |   6.87 MB |        1.04 |
| _FlameCsv_SrcGen_Parallel        | False |  1,163.9 μs |  3.24 μs |  3.03 μs |  0.36 |    0.00 | 43.9453 | 11.7188 |   6.87 MB |        1.04 |
| _Sylvan                          | False |  6,511.5 μs | 26.22 μs | 25.75 μs |  2.03 |    0.01 | 66.4063 |       - |  10.45 MB |        1.58 |
| _CsvHelper                       | False | 11,671.1 μs | 26.67 μs | 23.64 μs |  3.64 |    0.02 | 78.1250 |       - |  13.25 MB |        2.00 |
| _RecordParser_Hardcoded          | False |  3,051.5 μs | 16.05 μs | 15.01 μs |  0.95 |    0.01 | 46.8750 | 23.4375 |   7.46 MB |        1.13 |
| _RecordParser_Parallel_Hardcoded | False |  3,307.9 μs |  8.38 μs |  8.23 μs |  1.03 |    0.00 | 41.0156 |  5.8594 |   6.71 MB |        1.01 |
| _Sep                             | False |  3,572.0 μs | 11.52 μs | 11.32 μs |  1.11 |    0.01 | 41.0156 |       - |   6.62 MB |        1.00 |
| _Sep_Parallel                    | False |    990.9 μs |  1.68 μs |  1.40 μs |  0.31 |    0.00 | 42.9688 | 21.4844 |    6.7 MB |        1.01 |
| _Sep_Hardcoded                   | False |  3,404.3 μs | 12.25 μs | 11.46 μs |  1.06 |    0.01 | 41.0156 |       - |   6.62 MB |        1.00 |
| _Sep_Parallel_Hardcoded          | False |    988.8 μs |  2.59 μs |  2.30 μs |  0.31 |    0.00 | 42.9688 | 21.4844 |    6.7 MB |        1.01 |
|                                  |       |             |          |          |       |         |         |         |           |             |
| **_FlameCsv**                        | **True**  |  **3,289.1 μs** | **12.40 μs** | **12.18 μs** |  **1.00** |    **0.01** | **41.0156** |       **-** |   **6.61 MB** |        **1.00** |
| _Flame_SrcGen                    | True  |  3,290.3 μs |  8.97 μs |  8.81 μs |  1.00 |    0.00 | 41.0156 |       - |   6.61 MB |        1.00 |
| _FlameCsv_Reflection_Parallel    | True  |  1,067.7 μs |  1.73 μs |  1.53 μs |  0.32 |    0.00 | 43.9453 |  9.7656 |   6.88 MB |        1.04 |
| _FlameCsv_SrcGen_Parallel        | True  |  1,074.3 μs |  2.03 μs |  1.90 μs |  0.33 |    0.00 | 43.9453 | 10.7422 |   6.88 MB |        1.04 |
| _Sylvan                          | True  |  6,985.8 μs | 37.23 μs | 36.57 μs |  2.12 |    0.01 | 66.4063 |       - |  10.46 MB |        1.58 |
| _CsvHelper                       | True  | 12,272.3 μs | 28.69 μs | 26.83 μs |  3.73 |    0.02 | 78.1250 |       - |  13.28 MB |        2.01 |
| _RecordParser_Hardcoded          | True  |          NA |       NA |       NA |     ? |       ? |      NA |      NA |        NA |           ? |
| _RecordParser_Parallel_Hardcoded | True  |          NA |       NA |       NA |     ? |       ? |      NA |      NA |        NA |           ? |
| _Sep                             | True  |  3,745.9 μs | 14.48 μs | 14.22 μs |  1.14 |    0.01 | 41.0156 |       - |   6.62 MB |        1.00 |
| _Sep_Parallel                    | True  |          NA |       NA |       NA |     ? |       ? |      NA |      NA |        NA |           ? |
| _Sep_Hardcoded                   | True  |  3,442.1 μs |  9.37 μs |  9.20 μs |  1.05 |    0.00 | 41.0156 |       - |   6.62 MB |        1.00 |
| _Sep_Parallel_Hardcoded          | True  |          NA |       NA |       NA |     ? |       ? |      NA |      NA |        NA |           ? |

Benchmarks with issues:
  ReadObjects._RecordParser_Hardcoded: Job-CASMLP(MinIterationTime=1s, Server=True, MaxIterationCount=48, MaxWarmupIterationCount=32, MinIterationCount=16, MinWarmupIterationCount=16) [Async=True]
  ReadObjects._RecordParser_Parallel_Hardcoded: Job-CASMLP(MinIterationTime=1s, Server=True, MaxIterationCount=48, MaxWarmupIterationCount=32, MinIterationCount=16, MinWarmupIterationCount=16) [Async=True]
  ReadObjects._Sep_Parallel: Job-CASMLP(MinIterationTime=1s, Server=True, MaxIterationCount=48, MaxWarmupIterationCount=32, MinIterationCount=16, MinWarmupIterationCount=16) [Async=True]
  ReadObjects._Sep_Parallel_Hardcoded: Job-CASMLP(MinIterationTime=1s, Server=True, MaxIterationCount=48, MaxWarmupIterationCount=32, MinIterationCount=16, MinWarmupIterationCount=16) [Async=True]
