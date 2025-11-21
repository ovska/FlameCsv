```

BenchmarkDotNet v0.15.6, Windows 11 (10.0.26100.7171/24H2/2024Update/HudsonValley)
AMD Ryzen 7 PRO 7840U w/ Radeon 780M Graphics 3.30GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.0, 10.0.25.52411), X64 RyuJIT x86-64-v4
  Job-CASMLP : .NET 10.0.0 (10.0.0, 10.0.25.52411), X64 RyuJIT x86-64-v4

MinIterationTime=1s  Server=True  MaxIterationCount=48  
MaxWarmupIterationCount=32  MinIterationCount=16  MinWarmupIterationCount=16  

```
| Method        | Records | Async | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0      | Gen1    | Allocated  | Alloc Ratio |
|-------------- |-------- |------ |-----------:|----------:|----------:|------:|--------:|----------:|--------:|-----------:|------------:|
| **_Flame_SrcGen** | **20000**   | **False** |   **7.677 ms** | **0.0455 ms** | **0.0447 ms** |  **1.00** |    **0.01** |         **-** |       **-** |      **216 B** |        **1.00** |
| _Flame        | 20000   | False |   7.891 ms | 0.0686 ms | 0.0573 ms |  1.03 |    0.01 |         - |       - |      216 B |        1.00 |
| _Sep          | 20000   | False |   8.133 ms | 0.0463 ms | 0.0410 ms |  1.06 |    0.01 |    7.8125 |       - |   478640 B |    2,215.93 |
| _Sylvan       | 20000   | False |   7.621 ms | 0.0255 ms | 0.0250 ms |  0.99 |    0.01 |         - |       - |    33600 B |      155.56 |
| _CsvHelper    | 20000   | False |  14.568 ms | 0.1156 ms | 0.1081 ms |  1.90 |    0.02 |  203.1250 |       - |  7916547 B |   36,650.68 |
|               |         |       |            |           |           |       |         |           |         |            |             |
| **_Flame_SrcGen** | **20000**   | **True**  |   **9.773 ms** | **0.0363 ms** | **0.0340 ms** |  **1.00** |    **0.00** |         **-** |       **-** |    **30329 B** |        **1.00** |
| _Flame        | 20000   | True  |   9.848 ms | 0.0753 ms | 0.0740 ms |  1.01 |    0.01 |         - |       - |    30337 B |        1.00 |
| _Sep          | 20000   | True  | 144.516 ms | 1.0815 ms | 1.0116 ms | 14.79 |    0.11 | 1375.0000 |       - | 74732369 B |    2,464.06 |
| _Sylvan       | 20000   | True  |  10.203 ms | 0.0861 ms | 0.0845 ms |  1.04 |    0.01 |         - |       - |    83129 B |        2.74 |
| _CsvHelper    | 20000   | True  |  29.103 ms | 0.3191 ms | 0.3134 ms |  2.98 |    0.03 |  515.6250 | 15.6250 | 14318716 B |      472.11 |
