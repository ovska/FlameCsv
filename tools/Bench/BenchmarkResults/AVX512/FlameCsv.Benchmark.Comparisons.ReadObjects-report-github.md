```

BenchmarkDotNet v0.15.6, Windows 11 (10.0.26100.7171/24H2/2024Update/HudsonValley)
AMD Ryzen 7 PRO 7840U w/ Radeon 780M Graphics 3.30GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.0, 10.0.25.52411), X64 RyuJIT x86-64-v4
  Job-CASMLP : .NET 10.0.0 (10.0.0, 10.0.25.52411), X64 RyuJIT x86-64-v4

MinIterationTime=1s  Server=True  MaxIterationCount=48  
MaxWarmupIterationCount=32  MinIterationCount=16  MinWarmupIterationCount=16  

```
| Method        | Records | Async | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0      | Gen1    | Gen2   | Allocated  | Alloc Ratio |
|-------------- |-------- |------ |----------:|----------:|----------:|------:|--------:|----------:|--------:|-------:|-----------:|------------:|
| **_FlameCsv**     | **20000**   | **False** |  **4.858 ms** | **0.0265 ms** | **0.0248 ms** |  **1.05** |    **0.01** |  **203.1250** |  **3.9063** | **3.9063** |          **-** |        **0.00** |
| _Flame_SrcGen | 20000   | False |  4.647 ms | 0.0494 ms | 0.0485 ms |  1.00 |    0.01 |  183.5938 |       - |      - |  6935768 B |        1.00 |
| _Sylvan       | 20000   | False |  7.225 ms | 0.0974 ms | 0.0911 ms |  1.55 |    0.02 | 1425.7813 | 70.3125 |      - | 10961962 B |        1.58 |
| _CsvHelper    | 20000   | False | 15.745 ms | 0.0506 ms | 0.0423 ms |  3.39 |    0.04 |  375.0000 |       - |      - | 14365786 B |        2.07 |
|               |         |       |           |           |           |       |         |           |         |        |            |             |
| **_FlameCsv**     | **20000**   | **True**  |  **4.803 ms** | **0.0864 ms** | **0.0722 ms** |  **1.00** |    **0.02** |  **183.5938** |       **-** |      **-** |  **6935912 B** |        **1.00** |
| _Flame_SrcGen | 20000   | True  |  4.795 ms | 0.0683 ms | 0.0671 ms |  1.00 |    0.02 |  253.9063 |       - |      - |  6935712 B |        1.00 |
| _Sylvan       | 20000   | True  |  7.771 ms | 0.0975 ms | 0.0912 ms |  1.62 |    0.03 |  289.0625 |       - |      - | 10971846 B |        1.58 |
| _CsvHelper    | 20000   | True  | 16.928 ms | 0.3097 ms | 0.2746 ms |  3.53 |    0.07 |  531.2500 |       - |      - | 14406623 B |        2.08 |
