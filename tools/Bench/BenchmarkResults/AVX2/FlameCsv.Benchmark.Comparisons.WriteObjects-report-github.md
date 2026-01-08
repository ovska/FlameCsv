```

BenchmarkDotNet v0.15.8, Linux Arch Linux
AMD Ryzen 7 3700X 1.76GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.0, 42.42.42.42424), X64 RyuJIT x86-64-v3
  Job-CASMLP : .NET 10.0.0 (10.0.0, 42.42.42.42424), X64 RyuJIT x86-64-v3

MinIterationTime=1s  Server=True  MaxIterationCount=48  
MaxWarmupIterationCount=32  MinIterationCount=16  MinWarmupIterationCount=16  

```
| Method                     | Async | Mean       | Error     | StdDev    | Median     | Ratio | RatioSD | Gen0      | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|--------------------------- |------ |-----------:|----------:|----------:|-----------:|------:|--------:|----------:|---------:|---------:|-----------:|------------:|
| **_Flame_Reflection**          | **False** |  **10.717 ms** | **0.2320 ms** | **0.4580 ms** |  **10.433 ms** |  **1.00** |    **0.06** |         **-** |        **-** |        **-** |      **280 B** |        **1.00** |
| _Flame_SrcGen              | False |   9.948 ms | 0.2084 ms | 0.4114 ms |   9.633 ms |  0.93 |    0.05 |         - |        - |        - |      280 B |        1.00 |
| _Flame_SrcGen_Parallel     | False |   1.411 ms | 0.0088 ms | 0.0086 ms |   1.414 ms |  0.13 |    0.01 |    0.9766 |        - |        - |    72715 B |      259.70 |
| _Flame_Reflection_Parallel | False |   1.473 ms | 0.0109 ms | 0.0107 ms |   1.470 ms |  0.14 |    0.01 |    0.9766 |        - |        - |    72543 B |      259.08 |
| _Sylvan                    | False |  10.860 ms | 0.1027 ms | 0.1440 ms |  10.784 ms |  1.02 |    0.04 |         - |        - |        - |    33600 B |      120.00 |
| _CsvHelper                 | False |  23.316 ms | 0.4568 ms | 0.8467 ms |  23.811 ms |  2.18 |    0.12 |  203.1250 |        - |        - |  7920494 B |   28,287.48 |
| _Sep                       | False |  11.762 ms | 0.2385 ms | 0.4707 ms |  11.793 ms |  1.10 |    0.06 |    7.8125 |        - |        - |   478640 B |    1,709.43 |
| _RecordParser              | False |  18.826 ms | 0.1233 ms | 0.1210 ms |  18.846 ms |  1.76 |    0.07 |  640.6250 | 593.7500 |        - | 36743278 B |  131,225.99 |
| _RecordParser_Parallel     | False |  21.331 ms | 0.2618 ms | 0.2449 ms |  21.329 ms |  1.99 |    0.08 |  656.2500 | 359.3750 | 140.6250 | 30256142 B |  108,057.65 |
|                            |       |            |           |           |            |       |         |           |          |          |            |             |
| **_Flame_Reflection**          | **True**  |  **10.598 ms** | **0.0738 ms** | **0.0725 ms** |  **10.604 ms** |  **1.00** |    **0.01** |         **-** |        **-** |        **-** |    **28389 B** |        **1.00** |
| _Flame_SrcGen              | True  |  10.339 ms | 0.0549 ms | 0.0539 ms |  10.341 ms |  0.98 |    0.01 |         - |        - |        - |    28390 B |        1.00 |
| _Flame_SrcGen_Parallel     | True  |   1.320 ms | 0.0039 ms | 0.0038 ms |   1.319 ms |  0.12 |    0.00 |    7.8125 |        - |        - |   327983 B |       11.55 |
| _Flame_Reflection_Parallel | True  |   1.374 ms | 0.0042 ms | 0.0041 ms |   1.374 ms |  0.13 |    0.00 |    7.8125 |        - |        - |   328091 B |       11.56 |
| _Sylvan                    | True  |  11.967 ms | 0.0765 ms | 0.0751 ms |  11.962 ms |  1.13 |    0.01 |         - |        - |        - |    83144 B |        2.93 |
| _CsvHelper                 | True  |  35.768 ms | 0.3272 ms | 0.3214 ms |  35.883 ms |  3.38 |    0.04 |  375.0000 |        - |        - | 14322527 B |      504.51 |
| _Sep                       | True  | 266.176 ms | 5.2391 ms | 5.3802 ms | 266.866 ms | 25.12 |    0.52 | 2000.0000 |        - |        - | 74693112 B |    2,631.06 |
| _RecordParser              | True  |         NA |        NA |        NA |         NA |     ? |       ? |        NA |       NA |       NA |         NA |           ? |
| _RecordParser_Parallel     | True  |         NA |        NA |        NA |         NA |     ? |       ? |        NA |       NA |       NA |         NA |           ? |

Benchmarks with issues:
  WriteObjects._RecordParser: Job-CASMLP(MinIterationTime=1s, Server=True, MaxIterationCount=48, MaxWarmupIterationCount=32, MinIterationCount=16, MinWarmupIterationCount=16) [Async=True]
  WriteObjects._RecordParser_Parallel: Job-CASMLP(MinIterationTime=1s, Server=True, MaxIterationCount=48, MaxWarmupIterationCount=32, MinIterationCount=16, MinWarmupIterationCount=16) [Async=True]
