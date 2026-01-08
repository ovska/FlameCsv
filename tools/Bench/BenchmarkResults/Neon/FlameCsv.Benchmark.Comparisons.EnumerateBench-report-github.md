```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.2 (25C56) [Darwin 25.2.0]
Apple M4 Max, 1 CPU, 16 logical and 16 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.0, 10.0.25.52411), Arm64 RyuJIT armv8.0-a
  Job-CASMLP : .NET 10.0.0 (10.0.0, 10.0.25.52411), Arm64 RyuJIT armv8.0-a

MinIterationTime=1s  Server=True  MaxIterationCount=48  
MaxWarmupIterationCount=32  MinIterationCount=16  MinWarmupIterationCount=16  

```
| Method                 | Quoted | Async | Mean        | Error    | StdDev   | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|----------------------- |------- |------ |------------:|---------:|---------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **_FlameCsv**              | **False**  | **False** |    **890.1 μs** |  **4.52 μs** |  **4.23 μs** |  **1.00** |    **0.01** |        **-** |       **-** |      **512 B** |        **1.00** |
| _Sep                   | False  | False |  2,371.4 μs | 15.16 μs | 14.89 μs |  2.66 |    0.02 |        - |       - |     5856 B |       11.44 |
| _Sylvan                | False  | False |  9,319.4 μs | 50.47 μs | 49.57 μs | 10.47 |    0.07 |        - |       - |     8618 B |       16.83 |
| _CsvHelper             | False  | False | 23,369.4 μs | 42.85 μs | 42.09 μs | 26.26 |    0.13 | 218.7500 |       - | 37466888 B |   73,177.52 |
| _RecordParser          | False  | False |  5,673.3 μs | 24.79 μs | 24.35 μs |  6.37 |    0.04 | 230.4688 | 35.1563 | 38405141 B |   75,010.04 |
| _RecordParser_Parallel | False  | False |  6,455.8 μs | 22.67 μs | 22.27 μs |  7.25 |    0.04 | 226.5625 |  7.8125 | 37519703 B |   73,280.67 |
|                        |        |       |             |          |          |       |         |          |         |            |             |
| **_FlameCsv**              | **False**  | **True**  |  **1,063.7 μs** |  **1.78 μs** |  **1.75 μs** |  **1.00** |    **0.00** |        **-** |       **-** |      **552 B** |        **1.00** |
| _Sep                   | False  | True  |  2,472.9 μs | 12.96 μs | 12.72 μs |  2.32 |    0.01 |        - |       - |     5856 B |       10.61 |
| _Sylvan                | False  | True  |  9,565.2 μs | 29.01 μs | 24.23 μs |  8.99 |    0.03 |        - |       - |    44618 B |       80.83 |
| _CsvHelper             | False  | True  | 24,387.6 μs | 34.99 μs | 34.37 μs | 22.93 |    0.05 | 218.7500 |       - | 37612760 B |   68,139.06 |
| _RecordParser          | False  | True  |          NA |       NA |       NA |     ? |       ? |       NA |      NA |         NA |           ? |
| _RecordParser_Parallel | False  | True  |          NA |       NA |       NA |     ? |       ? |       NA |      NA |         NA |           ? |
|                        |        |       |             |          |          |       |         |          |         |            |             |
| **_FlameCsv**              | **True**   | **False** |  **2,267.6 μs** | **16.03 μs** | **15.75 μs** |  **1.00** |    **0.01** |        **-** |       **-** |      **512 B** |        **1.00** |
| _Sep                   | True   | False |  4,232.0 μs | 19.58 μs | 19.23 μs |  1.87 |    0.01 |        - |       - |     5576 B |       10.89 |
| _Sylvan                | True   | False | 20,332.0 μs | 75.68 μs | 74.32 μs |  8.97 |    0.07 |        - |       - |     8059 B |       15.74 |
| _CsvHelper             | True   | False | 45,591.4 μs | 96.82 μs | 95.09 μs | 20.11 |    0.14 | 375.0000 |       - | 62892032 B |  122,836.00 |
| _RecordParser          | True   | False |  9,679.4 μs | 95.55 μs | 93.84 μs |  4.27 |    0.05 | 390.6250 | 70.3125 | 65003287 B |  126,959.54 |
| _RecordParser_Parallel | True   | False | 10,813.1 μs | 22.72 μs | 22.31 μs |  4.77 |    0.03 | 375.0000 | 15.6250 | 62102184 B |  121,293.33 |
|                        |        |       |             |          |          |       |         |          |         |            |             |
| **_FlameCsv**              | **True**   | **True**  |  **2,402.4 μs** |  **9.63 μs** |  **9.46 μs** |  **1.00** |    **0.01** |        **-** |       **-** |      **552 B** |        **1.00** |
| _Sep                   | True   | True  |  4,478.1 μs | 34.00 μs | 33.40 μs |  1.86 |    0.02 |        - |       - |     5576 B |       10.10 |
| _Sylvan                | True   | True  | 21,032.6 μs | 63.50 μs | 62.37 μs |  8.75 |    0.04 |        - |       - |    84091 B |      152.34 |
| _CsvHelper             | True   | True  | 46,854.6 μs | 86.63 μs | 85.08 μs | 19.50 |    0.08 | 375.0000 |       - | 63201416 B |  114,495.32 |
| _RecordParser          | True   | True  |          NA |       NA |       NA |     ? |       ? |       NA |      NA |         NA |           ? |
| _RecordParser_Parallel | True   | True  |          NA |       NA |       NA |     ? |       ? |       NA |      NA |         NA |           ? |

Benchmarks with issues:
  EnumerateBench._RecordParser: Job-CASMLP(MinIterationTime=1s, Server=True, MaxIterationCount=48, MaxWarmupIterationCount=32, MinIterationCount=16, MinWarmupIterationCount=16) [Quoted=False, Async=True]
  EnumerateBench._RecordParser_Parallel: Job-CASMLP(MinIterationTime=1s, Server=True, MaxIterationCount=48, MaxWarmupIterationCount=32, MinIterationCount=16, MinWarmupIterationCount=16) [Quoted=False, Async=True]
  EnumerateBench._RecordParser: Job-CASMLP(MinIterationTime=1s, Server=True, MaxIterationCount=48, MaxWarmupIterationCount=32, MinIterationCount=16, MinWarmupIterationCount=16) [Quoted=True, Async=True]
  EnumerateBench._RecordParser_Parallel: Job-CASMLP(MinIterationTime=1s, Server=True, MaxIterationCount=48, MaxWarmupIterationCount=32, MinIterationCount=16, MinWarmupIterationCount=16) [Quoted=True, Async=True]
