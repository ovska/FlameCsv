```

BenchmarkDotNet v0.15.8, Linux Arch Linux
AMD Ryzen 7 3700X 1.76GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.0, 42.42.42.42424), X64 RyuJIT x86-64-v3
  Job-CASMLP : .NET 10.0.0 (10.0.0, 42.42.42.42424), X64 RyuJIT x86-64-v3

MinIterationTime=1s  Server=True  MaxIterationCount=48  
MaxWarmupIterationCount=32  MinIterationCount=16  MinWarmupIterationCount=16  

```
| Method                     | Async | Mean       | Error     | StdDev    | Median     | Ratio | RatioSD | Gen0      | Gen1     | Gen2     | Allocated   | Alloc Ratio |
|--------------------------- |------ |-----------:|----------:|----------:|-----------:|------:|--------:|----------:|---------:|---------:|------------:|------------:|
| **_Flame_Reflection**          | **False** |  **10.311 ms** | **0.0518 ms** | **0.0636 ms** |  **10.280 ms** |  **1.00** |    **0.01** |         **-** |        **-** |        **-** |       **320 B** |        **1.00** |
| _Flame_SrcGen              | False |   9.793 ms | 0.1781 ms | 0.3025 ms |   9.638 ms |  0.95 |    0.03 |         - |        - |        - |       320 B |        1.00 |
| _Flame_SrcGen_Parallel     | False |   1.347 ms | 0.0266 ms | 0.0373 ms |   1.361 ms |  0.13 |    0.00 |    0.9766 |        - |        - |     68056 B |      212.68 |
| _Flame_Reflection_Parallel | False |   1.362 ms | 0.0269 ms | 0.0368 ms |   1.367 ms |  0.13 |    0.00 |    0.9766 |        - |        - |     66772 B |      208.66 |
| _Sylvan                    | False |  10.406 ms | 0.1635 ms | 0.1366 ms |  10.357 ms |  1.01 |    0.01 |         - |        - |        - |     33600 B |      105.00 |
| _CsvHelper                 | False |  22.753 ms | 0.4863 ms | 0.9599 ms |  22.361 ms |  2.21 |    0.09 |  250.0000 |  46.8750 |  31.2500 | 146891327 B |  459,035.40 |
| _Sep                       | False |  11.833 ms | 0.2646 ms | 0.5223 ms |  11.440 ms |  1.15 |    0.05 |    7.8125 |        - |        - |    478640 B |    1,495.75 |
| _RecordParser              | False |  18.911 ms | 0.2314 ms | 0.2273 ms |  18.974 ms |  1.83 |    0.02 |  656.2500 | 578.1250 |  15.6250 |  35673561 B |  111,479.88 |
| _RecordParser_Parallel     | False |  21.483 ms | 0.3811 ms | 0.3743 ms |  21.490 ms |  2.08 |    0.04 |  671.8750 | 328.1250 | 140.6250 |  30873488 B |   96,479.65 |
|                            |       |            |           |           |            |       |         |           |          |          |             |             |
| **_Flame_Reflection**          | **True**  |  **10.418 ms** | **0.0628 ms** | **0.0616 ms** |  **10.406 ms** |  **1.00** |    **0.01** |         **-** |        **-** |        **-** |     **28392 B** |        **1.00** |
| _Flame_SrcGen              | True  |  10.629 ms | 0.0998 ms | 0.0980 ms |  10.614 ms |  1.02 |    0.01 |         - |        - |        - |     28392 B |        1.00 |
| _Flame_SrcGen_Parallel     | True  |   1.283 ms | 0.0069 ms | 0.0068 ms |   1.285 ms |  0.12 |    0.00 |    3.9063 |        - |        - |    155417 B |        5.47 |
| _Flame_Reflection_Parallel | True  |   1.345 ms | 0.0041 ms | 0.0038 ms |   1.344 ms |  0.13 |    0.00 |    3.9063 |        - |        - |    154126 B |        5.43 |
| _Sylvan                    | True  |  11.730 ms | 0.1700 ms | 0.1669 ms |  11.668 ms |  1.13 |    0.02 |         - |        - |        - |     83144 B |        2.93 |
| _CsvHelper                 | True  |  36.213 ms | 0.3194 ms | 0.3137 ms |  36.120 ms |  3.48 |    0.04 |  375.0000 |        - |        - |  14322541 B |      504.46 |
| _Sep                       | True  | 255.498 ms | 5.0005 ms | 9.3921 ms | 258.880 ms | 24.53 |    0.90 | 2000.0000 |        - |        - |  74714768 B |    2,631.54 |
| _RecordParser              | True  |         NA |        NA |        NA |         NA |     ? |       ? |        NA |       NA |       NA |          NA |           ? |
| _RecordParser_Parallel     | True  |         NA |        NA |        NA |         NA |     ? |       ? |        NA |       NA |       NA |          NA |           ? |

Benchmarks with issues:
  WriteObjects._RecordParser: Job-CASMLP(MinIterationTime=1s, Server=True, MaxIterationCount=48, MaxWarmupIterationCount=32, MinIterationCount=16, MinWarmupIterationCount=16) [Async=True]
  WriteObjects._RecordParser_Parallel: Job-CASMLP(MinIterationTime=1s, Server=True, MaxIterationCount=48, MaxWarmupIterationCount=32, MinIterationCount=16, MinWarmupIterationCount=16) [Async=True]
