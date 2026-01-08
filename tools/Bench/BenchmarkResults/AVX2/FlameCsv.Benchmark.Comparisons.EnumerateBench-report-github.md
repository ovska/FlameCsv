```

BenchmarkDotNet v0.15.8, Linux Arch Linux
AMD Ryzen 7 3700X 1.76GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.0, 42.42.42.42424), X64 RyuJIT x86-64-v3
  Job-CASMLP : .NET 10.0.0 (10.0.0, 42.42.42.42424), X64 RyuJIT x86-64-v3

MinIterationTime=1s  Server=True  MaxIterationCount=48  
MaxWarmupIterationCount=32  MinIterationCount=16  MinWarmupIterationCount=16  

```
| Method                 | Quoted | Async | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Gen0       | Gen1     | Allocated  | Alloc Ratio |
|----------------------- |------- |------ |----------:|----------:|----------:|----------:|------:|--------:|-----------:|---------:|-----------:|------------:|
| **_FlameCsv**              | **False**  | **False** |  **1.808 ms** | **0.0210 ms** | **0.0197 ms** |  **1.802 ms** |  **1.00** |    **0.01** |          **-** |        **-** |      **512 B** |        **1.00** |
| _Sep                   | False  | False |  3.372 ms | 0.0056 ms | 0.0047 ms |  3.371 ms |  1.86 |    0.02 |          - |        - |     5936 B |       11.59 |
| _Sylvan                | False  | False |  6.088 ms | 0.1168 ms | 0.1599 ms |  6.017 ms |  3.37 |    0.09 |          - |        - |     9233 B |       18.03 |
| _CsvHelper             | False  | False | 46.882 ms | 0.3887 ms | 0.3246 ms | 46.785 ms | 25.93 |    0.32 | 14312.5000 |        - | 37466888 B |   73,177.52 |
| _RecordParser          | False  | False | 11.317 ms | 0.1069 ms | 0.1050 ms | 11.335 ms |  6.26 |    0.09 |  1039.0625 | 179.6875 | 38682968 B |   75,552.67 |
| _RecordParser_Parallel | False  | False | 16.821 ms | 0.3205 ms | 0.3292 ms | 16.822 ms |  9.30 |    0.20 |  2125.0000 |  78.1250 | 37533880 B |   73,308.36 |
|                        |        |       |           |           |           |           |       |         |            |          |            |             |
| **_FlameCsv**              | **False**  | **True**  |  **2.104 ms** | **0.0446 ms** | **0.0880 ms** |  **2.042 ms** |  **1.00** |    **0.06** |          **-** |        **-** |      **552 B** |        **1.00** |
| _Sep                   | False  | True  |  3.625 ms | 0.0146 ms | 0.0179 ms |  3.621 ms |  1.73 |    0.07 |          - |        - |     5936 B |       10.75 |
| _Sylvan                | False  | True  |  7.136 ms | 0.0839 ms | 0.0700 ms |  7.097 ms |  3.40 |    0.14 |          - |        - |    45234 B |       81.95 |
| _CsvHelper             | False  | True  | 47.926 ms | 0.7761 ms | 0.6880 ms | 47.500 ms | 22.82 |    0.97 | 14375.0000 |  31.2500 | 37612760 B |   68,139.06 |
| _RecordParser          | False  | True  |        NA |        NA |        NA |        NA |     ? |       ? |         NA |       NA |         NA |           ? |
| _RecordParser_Parallel | False  | True  |        NA |        NA |        NA |        NA |     ? |       ? |         NA |       NA |         NA |           ? |
|                        |        |       |           |           |           |           |       |         |            |          |            |             |
| **_FlameCsv**              | **True**   | **False** |  **4.358 ms** | **0.0964 ms** | **0.1902 ms** |  **4.306 ms** |  **1.00** |    **0.06** |          **-** |        **-** |      **512 B** |        **1.00** |
| _Sep                   | True   | False |  6.045 ms | 0.1311 ms | 0.2588 ms |  5.879 ms |  1.39 |    0.08 |          - |        - |     5656 B |       11.05 |
| _Sylvan                | True   | False | 11.787 ms | 0.2685 ms | 0.5300 ms | 11.509 ms |  2.71 |    0.17 |          - |        - |     8674 B |       16.94 |
| _CsvHelper             | True   | False | 97.407 ms | 0.4247 ms | 0.3973 ms | 97.448 ms | 22.39 |    0.95 |  1636.3636 |        - | 62892032 B |  122,836.00 |
| _RecordParser          | True   | False | 19.867 ms | 0.1507 ms | 0.1480 ms | 19.843 ms |  4.57 |    0.20 |  1687.5000 | 171.8750 | 63249617 B |  123,534.41 |
| _RecordParser_Parallel | True   | False | 25.553 ms | 0.3524 ms | 0.3461 ms | 25.554 ms |  5.87 |    0.26 |  6109.3750 | 234.3750 | 62186543 B |  121,458.09 |
|                        |        |       |           |           |           |           |       |         |            |          |            |             |
| **_FlameCsv**              | **True**   | **True**  |  **4.605 ms** | **0.0870 ms** | **0.0855 ms** |  **4.567 ms** |  **1.00** |    **0.03** |          **-** |        **-** |      **552 B** |        **1.00** |
| _Sep                   | True   | True  |  6.727 ms | 0.1437 ms | 0.2837 ms |  6.588 ms |  1.46 |    0.07 |          - |        - |     5656 B |       10.25 |
| _Sylvan                | True   | True  | 13.215 ms | 0.3066 ms | 0.6052 ms | 12.805 ms |  2.87 |    0.14 |          - |        - |    84706 B |      153.45 |
| _CsvHelper             | True   | True  | 96.591 ms | 1.9124 ms | 2.0462 ms | 97.578 ms | 20.98 |    0.57 |  1666.6667 |        - | 63201416 B |  114,495.32 |
| _RecordParser          | True   | True  |        NA |        NA |        NA |        NA |     ? |       ? |         NA |       NA |         NA |           ? |
| _RecordParser_Parallel | True   | True  |        NA |        NA |        NA |        NA |     ? |       ? |         NA |       NA |         NA |           ? |

Benchmarks with issues:
  EnumerateBench._RecordParser: Job-CASMLP(MinIterationTime=1s, Server=True, MaxIterationCount=48, MaxWarmupIterationCount=32, MinIterationCount=16, MinWarmupIterationCount=16) [Quoted=False, Async=True]
  EnumerateBench._RecordParser_Parallel: Job-CASMLP(MinIterationTime=1s, Server=True, MaxIterationCount=48, MaxWarmupIterationCount=32, MinIterationCount=16, MinWarmupIterationCount=16) [Quoted=False, Async=True]
  EnumerateBench._RecordParser: Job-CASMLP(MinIterationTime=1s, Server=True, MaxIterationCount=48, MaxWarmupIterationCount=32, MinIterationCount=16, MinWarmupIterationCount=16) [Quoted=True, Async=True]
  EnumerateBench._RecordParser_Parallel: Job-CASMLP(MinIterationTime=1s, Server=True, MaxIterationCount=48, MaxWarmupIterationCount=32, MinIterationCount=16, MinWarmupIterationCount=16) [Quoted=True, Async=True]
