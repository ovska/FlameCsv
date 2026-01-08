```

BenchmarkDotNet v0.15.8, Linux Arch Linux
AMD Ryzen 7 3700X 1.76GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.0, 42.42.42.42424), X64 RyuJIT x86-64-v3
  Job-CASMLP : .NET 10.0.0 (10.0.0, 42.42.42.42424), X64 RyuJIT x86-64-v3

MinIterationTime=1s  Server=True  MaxIterationCount=48  
MaxWarmupIterationCount=32  MinIterationCount=16  MinWarmupIterationCount=16  

```
| Method                           | Async | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Gen0      | Gen1     | Gen2    | Allocated  | Alloc Ratio |
|--------------------------------- |------ |----------:|----------:|----------:|----------:|------:|--------:|----------:|---------:|--------:|-----------:|------------:|
| **_FlameCsv**                        | **False** |  **8.100 ms** | **0.1587 ms** | **0.1484 ms** |  **8.161 ms** |  **1.00** |    **0.03** |  **437.5000** |   **7.8125** |  **7.8125** |          **-** |          **NA** |
| _Flame_SrcGen                    | False |  8.102 ms | 0.0288 ms | 0.0283 ms |  8.092 ms |  1.00 |    0.02 |  265.6250 |   7.8125 |  7.8125 |          - |          NA |
| _FlameCsv_Reflection_Parallel    | False |  2.570 ms | 0.0246 ms | 0.0241 ms |  2.580 ms |  0.32 |    0.01 |  199.2188 |  27.3438 |       - |  7204739 B |          NA |
| _FlameCsv_SrcGen_Parallel        | False |  2.644 ms | 0.0229 ms | 0.0225 ms |  2.633 ms |  0.33 |    0.01 |  199.2188 |  29.2969 |       - |  7204807 B |          NA |
| _Sylvan                          | False | 11.080 ms | 0.1317 ms | 0.1294 ms | 11.122 ms |  1.37 |    0.03 |  289.0625 |        - |       - | 10961814 B |          NA |
| _CsvHelper                       | False | 23.456 ms | 0.0511 ms | 0.0453 ms | 23.440 ms |  2.90 |    0.05 | 5312.5000 |  46.8750 |       - | 13891630 B |          NA |
| _RecordParser_Hardcoded          | False |  6.964 ms | 0.0271 ms | 0.0266 ms |  6.964 ms |  0.86 |    0.02 |  199.2188 |  85.9375 |  3.9063 |  7392662 B |          NA |
| _RecordParser_Parallel_Hardcoded | False |  7.568 ms | 0.0474 ms | 0.0465 ms |  7.553 ms |  0.93 |    0.02 |  226.5625 |  35.1563 |  7.8125 | 34259077 B |          NA |
| _Sep                             | False |  8.713 ms | 0.1703 ms | 0.2798 ms |  8.836 ms |  1.08 |    0.04 |  203.1250 |   7.8125 |  7.8125 |          - |          NA |
| _Sep_Parallel                    | False |  2.583 ms | 0.0104 ms | 0.0102 ms |  2.583 ms |  0.32 |    0.01 |  125.0000 | 123.0469 |       - |  7021098 B |          NA |
| _Sep_Hardcoded                   | False |  7.593 ms | 0.1770 ms | 0.3493 ms |  7.475 ms |  0.94 |    0.05 | 1351.5625 |   7.8125 |       - |  6939912 B |          NA |
| _Sep_Parallel_Hardcoded          | False |  2.522 ms | 0.0063 ms | 0.0062 ms |  2.521 ms |  0.31 |    0.01 |  126.9531 | 125.0000 |       - |  7020940 B |          NA |
|                                  |       |           |           |           |           |       |         |           |          |         |            |             |
| **_FlameCsv**                        | **True**  |  **8.078 ms** | **0.0110 ms** | **0.0103 ms** |  **8.076 ms** |  **1.00** |    **0.00** |  **179.6875** |        **-** |       **-** |  **6935888 B** |        **1.00** |
| _Flame_SrcGen                    | True  |  7.776 ms | 0.0207 ms | 0.0203 ms |  7.776 ms |  0.96 |    0.00 |  179.6875 |        - |       - |  6935904 B |        1.00 |
| _FlameCsv_Reflection_Parallel    | True  |  2.563 ms | 0.0165 ms | 0.0162 ms |  2.563 ms |  0.32 |    0.00 |  277.3438 |  48.8281 |       - |  7219783 B |        1.04 |
| _FlameCsv_SrcGen_Parallel        | True  |  2.499 ms | 0.0169 ms | 0.0166 ms |  2.494 ms |  0.31 |    0.00 |  255.8594 |  42.9688 |       - |  7219996 B |        1.04 |
| _Sylvan                          | True  | 12.407 ms | 0.2815 ms | 0.5556 ms | 12.725 ms |  1.54 |    0.07 | 2140.6250 |  85.9375 |  7.8125 | 10971997 B |        1.58 |
| _CsvHelper                       | True  | 25.699 ms | 0.5054 ms | 0.7565 ms | 26.128 ms |  3.18 |    0.09 |  375.0000 |        - |       - | 13929976 B |        2.01 |
| _RecordParser_Hardcoded          | True  |        NA |        NA |        NA |        NA |     ? |       ? |        NA |       NA |      NA |         NA |           ? |
| _RecordParser_Parallel_Hardcoded | True  |        NA |        NA |        NA |        NA |     ? |       ? |        NA |       NA |      NA |         NA |           ? |
| _Sep                             | True  |  9.106 ms | 0.0947 ms | 0.0930 ms |  9.133 ms |  1.13 |    0.01 |  257.8125 |  15.6250 | 15.6250 | 19752765 B |        2.85 |
| _Sep_Parallel                    | True  |        NA |        NA |        NA |        NA |     ? |       ? |        NA |       NA |      NA |         NA |           ? |
| _Sep_Hardcoded                   | True  |  8.206 ms | 0.0274 ms | 0.0269 ms |  8.198 ms |  1.02 |    0.00 |  210.9375 |   7.8125 |  7.8125 |          - |        0.00 |
| _Sep_Parallel_Hardcoded          | True  |        NA |        NA |        NA |        NA |     ? |       ? |        NA |       NA |      NA |         NA |           ? |

Benchmarks with issues:
  ReadObjects._RecordParser_Hardcoded: Job-CASMLP(MinIterationTime=1s, Server=True, MaxIterationCount=48, MaxWarmupIterationCount=32, MinIterationCount=16, MinWarmupIterationCount=16) [Async=True]
  ReadObjects._RecordParser_Parallel_Hardcoded: Job-CASMLP(MinIterationTime=1s, Server=True, MaxIterationCount=48, MaxWarmupIterationCount=32, MinIterationCount=16, MinWarmupIterationCount=16) [Async=True]
  ReadObjects._Sep_Parallel: Job-CASMLP(MinIterationTime=1s, Server=True, MaxIterationCount=48, MaxWarmupIterationCount=32, MinIterationCount=16, MinWarmupIterationCount=16) [Async=True]
  ReadObjects._Sep_Parallel_Hardcoded: Job-CASMLP(MinIterationTime=1s, Server=True, MaxIterationCount=48, MaxWarmupIterationCount=32, MinIterationCount=16, MinWarmupIterationCount=16) [Async=True]
