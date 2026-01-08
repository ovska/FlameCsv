```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.2 (25C56) [Darwin 25.2.0]
Apple M4 Max, 1 CPU, 16 logical and 16 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.0, 10.0.25.52411), Arm64 RyuJIT armv8.0-a
  Job-CASMLP : .NET 10.0.0 (10.0.0, 10.0.25.52411), Arm64 RyuJIT armv8.0-a

MinIterationTime=1s  Server=True  MaxIterationCount=48  
MaxWarmupIterationCount=32  MinIterationCount=16  MinWarmupIterationCount=16  

```
| Method                     | Async | Mean         | Error     | StdDev    | Median       | Ratio | RatioSD | Gen0     | Gen1    | Gen2    | Allocated  | Alloc Ratio |
|--------------------------- |------ |-------------:|----------:|----------:|-------------:|------:|--------:|---------:|--------:|--------:|-----------:|------------:|
| **_Flame_Reflection**          | **False** |   **4,859.4 μs** |  **17.20 μs** |  **16.90 μs** |   **4,859.4 μs** |  **1.00** |    **0.00** |        **-** |       **-** |       **-** |      **280 B** |        **1.00** |
| _Flame_SrcGen              | False |   4,633.8 μs |  31.41 μs |  30.85 μs |   4,622.1 μs |  0.95 |    0.01 |        - |       - |       - |      280 B |        1.00 |
| _Flame_SrcGen_Parallel     | False |     527.7 μs |   9.53 μs |  12.39 μs |     523.6 μs |  0.11 |    0.00 |        - |       - |       - |    78409 B |      280.03 |
| _Flame_Reflection_Parallel | False |     559.2 μs |  11.22 μs |  21.63 μs |     548.0 μs |  0.12 |    0.00 |        - |       - |       - |    78552 B |      280.54 |
| _Sylvan                    | False |   5,339.7 μs |  70.48 μs |  69.22 μs |   5,303.9 μs |  1.10 |    0.01 |        - |       - |       - |    33600 B |      120.00 |
| _CsvHelper                 | False |   8,931.5 μs |  12.23 μs |  11.44 μs |   8,930.1 μs |  1.84 |    0.01 |  46.8750 |       - |       - |  7919967 B |   28,285.60 |
| _Sep                       | False |   4,967.3 μs |  19.62 μs |  16.39 μs |   4,968.7 μs |  1.02 |    0.00 |        - |       - |       - |   478640 B |    1,709.43 |
| _RecordParser              | False |  14,983.3 μs |  87.57 μs |  86.00 μs |  14,992.3 μs |  3.08 |    0.02 |  54.6875 | 23.4375 |       - | 37799861 B |  134,999.50 |
| _RecordParser_Parallel     | False |   9,271.6 μs |  45.99 μs |  45.17 μs |   9,270.7 μs |  1.91 |    0.01 |  39.0625 | 31.2500 | 31.2500 | 31690597 B |  113,180.70 |
|                            |       |              |           |           |              |       |         |          |         |         |            |             |
| **_Flame_Reflection**          | **True**  |   **5,489.1 μs** |  **10.41 μs** |  **10.22 μs** |   **5,491.1 μs** |  **1.00** |    **0.00** |        **-** |       **-** |       **-** |    **28392 B** |        **1.00** |
| _Flame_SrcGen              | True  |   5,507.9 μs |  10.05 μs |   9.40 μs |   5,509.8 μs |  1.00 |    0.00 |        - |       - |       - |    28392 B |        1.00 |
| _Flame_SrcGen_Parallel     | True  |     623.2 μs |   1.25 μs |   1.17 μs |     623.1 μs |  0.11 |    0.00 |   1.9531 |       - |       - |   326744 B |       11.51 |
| _Flame_Reflection_Parallel | True  |     634.1 μs |   1.32 μs |   1.23 μs |     633.9 μs |  0.12 |    0.00 |   1.9531 |       - |       - |   326327 B |       11.49 |
| _Sylvan                    | True  |   6,881.6 μs |  73.72 μs |  72.40 μs |   6,888.9 μs |  1.25 |    0.01 |        - |       - |       - |    83143 B |        2.93 |
| _CsvHelper                 | True  |  14,453.1 μs | 105.97 μs | 104.08 μs |  14,409.6 μs |  2.63 |    0.02 |  85.9375 |  7.8125 |       - | 14321643 B |      504.43 |
| _Sep                       | True  | 116,183.3 μs | 944.48 μs | 927.61 μs | 116,142.1 μs | 21.17 |    0.17 | 444.4444 |       - |       - | 74754250 B |    2,632.93 |
| _RecordParser              | True  |           NA |        NA |        NA |           NA |     ? |       ? |       NA |      NA |      NA |         NA |           ? |
| _RecordParser_Parallel     | True  |           NA |        NA |        NA |           NA |     ? |       ? |       NA |      NA |      NA |         NA |           ? |

Benchmarks with issues:
  WriteObjects._RecordParser: Job-CASMLP(MinIterationTime=1s, Server=True, MaxIterationCount=48, MaxWarmupIterationCount=32, MinIterationCount=16, MinWarmupIterationCount=16) [Async=True]
  WriteObjects._RecordParser_Parallel: Job-CASMLP(MinIterationTime=1s, Server=True, MaxIterationCount=48, MaxWarmupIterationCount=32, MinIterationCount=16, MinWarmupIterationCount=16) [Async=True]
