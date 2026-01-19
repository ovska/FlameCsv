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
| **_FlameCsv**                        | **False** |  **7.023 ms** | **0.1047 ms** | **0.0979 ms** |  **6.968 ms** |  **1.00** |    **0.02** | **1355.4688** |   **3.9063** |       **-** |  **6935864 B** |        **1.00** |
| _Flame_SrcGen                    | False |  7.386 ms | 0.1984 ms | 0.3916 ms |  7.409 ms |  1.05 |    0.06 | 1355.4688 |   3.9063 |       - |  6935880 B |        1.00 |
| _FlameCsv_Reflection_Parallel    | False |  1.841 ms | 0.0354 ms | 0.0379 ms |  1.841 ms |  0.26 |    0.01 |  197.2656 |  39.0625 |       - |  7229215 B |        1.04 |
| _FlameCsv_SrcGen_Parallel        | False |  1.759 ms | 0.0242 ms | 0.0237 ms |  1.758 ms |  0.25 |    0.00 |  197.2656 |  36.1328 |       - |  7228803 B |        1.04 |
| _Sylvan                          | False | 11.178 ms | 0.2792 ms | 0.5511 ms | 11.109 ms |  1.59 |    0.08 | 2132.8125 |  93.7500 |  7.8125 | 10961965 B |        1.58 |
| _CsvHelper                       | False | 24.640 ms | 0.4843 ms | 0.9330 ms | 24.733 ms |  3.51 |    0.14 |  421.8750 |  15.6250 | 15.6250 |          - |        0.00 |
| _RecordParser_Hardcoded          | False |  7.187 ms | 0.0485 ms | 0.0476 ms |  7.186 ms |  1.02 |    0.02 |  203.1250 | 101.5625 |       - |  7447408 B |        1.07 |
| _RecordParser_Parallel_Hardcoded | False |  7.600 ms | 0.1042 ms | 0.1024 ms |  7.573 ms |  1.08 |    0.02 |  187.5000 |  15.6250 |       - |  7047498 B |        1.02 |
| _Sep                             | False |  8.808 ms | 0.1758 ms | 0.2159 ms |  8.872 ms |  1.25 |    0.03 |  203.1250 |  15.6250 | 15.6250 |  6939928 B |        1.00 |
| _Sep_Parallel                    | False |  2.721 ms | 0.0237 ms | 0.0222 ms |  2.717 ms |  0.39 |    0.01 |  125.0000 | 123.0469 |       - |  7021268 B |        1.01 |
| _Sep_Hardcoded                   | False |  8.196 ms | 0.1626 ms | 0.2434 ms |  8.319 ms |  1.17 |    0.04 |  203.1250 |   7.8125 |  7.8125 |          - |        0.00 |
| _Sep_Parallel_Hardcoded          | False |  2.664 ms | 0.0293 ms | 0.0274 ms |  2.659 ms |  0.38 |    0.01 |  125.0000 | 123.0469 |       - |  7021060 B |        1.01 |
|                                  |       |           |           |           |           |       |         |           |          |         |            |             |
| **_FlameCsv**                        | **True**  |  **7.877 ms** | **0.1558 ms** | **0.2425 ms** |  **7.975 ms** |  **1.00** |    **0.04** |  **191.4063** |   **7.8125** |  **7.8125** |  **6935882 B** |        **1.00** |
| _Flame_SrcGen                    | True  |  7.515 ms | 0.1484 ms | 0.2519 ms |  7.484 ms |  0.95 |    0.04 |  179.6875 |        - |       - |  6935880 B |        1.00 |
| _FlameCsv_Reflection_Parallel    | True  |  1.894 ms | 0.0378 ms | 0.0464 ms |  1.901 ms |  0.24 |    0.01 |  197.2656 |  38.0859 |       - |  7229953 B |        1.04 |
| _FlameCsv_SrcGen_Parallel        | True  |  1.818 ms | 0.0192 ms | 0.0179 ms |  1.818 ms |  0.23 |    0.01 |  197.2656 |  35.1563 |       - |  7230110 B |        1.04 |
| _Sylvan                          | True  | 12.267 ms | 0.3287 ms | 0.6489 ms | 12.668 ms |  1.56 |    0.10 | 2140.6250 |  93.7500 |  7.8125 | 10971997 B |        1.58 |
| _CsvHelper                       | True  | 23.971 ms | 0.1014 ms | 0.0846 ms | 23.988 ms |  3.05 |    0.10 |  375.0000 |        - |       - | 13929976 B |        2.01 |
| _RecordParser_Hardcoded          | True  |        NA |        NA |        NA |        NA |     ? |       ? |        NA |       NA |      NA |         NA |           ? |
| _RecordParser_Parallel_Hardcoded | True  |        NA |        NA |        NA |        NA |     ? |       ? |        NA |       NA |      NA |         NA |           ? |
| _Sep                             | True  |  8.186 ms | 0.1042 ms | 0.0923 ms |  8.194 ms |  1.04 |    0.03 | 1351.5625 |   7.8125 |       - |  6939912 B |        1.00 |
| _Sep_Parallel                    | True  |        NA |        NA |        NA |        NA |     ? |       ? |        NA |       NA |      NA |         NA |           ? |
| _Sep_Hardcoded                   | True  |  7.410 ms | 0.0580 ms | 0.0543 ms |  7.390 ms |  0.94 |    0.03 | 1355.4688 |  11.7188 |       - |  6939912 B |        1.00 |
| _Sep_Parallel_Hardcoded          | True  |        NA |        NA |        NA |        NA |     ? |       ? |        NA |       NA |      NA |         NA |           ? |

Benchmarks with issues:
  ReadObjects._RecordParser_Hardcoded: Job-CASMLP(MinIterationTime=1s, Server=True, MaxIterationCount=48, MaxWarmupIterationCount=32, MinIterationCount=16, MinWarmupIterationCount=16) [Async=True]
  ReadObjects._RecordParser_Parallel_Hardcoded: Job-CASMLP(MinIterationTime=1s, Server=True, MaxIterationCount=48, MaxWarmupIterationCount=32, MinIterationCount=16, MinWarmupIterationCount=16) [Async=True]
  ReadObjects._Sep_Parallel: Job-CASMLP(MinIterationTime=1s, Server=True, MaxIterationCount=48, MaxWarmupIterationCount=32, MinIterationCount=16, MinWarmupIterationCount=16) [Async=True]
  ReadObjects._Sep_Parallel_Hardcoded: Job-CASMLP(MinIterationTime=1s, Server=True, MaxIterationCount=48, MaxWarmupIterationCount=32, MinIterationCount=16, MinWarmupIterationCount=16) [Async=True]
