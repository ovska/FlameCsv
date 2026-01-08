```

BenchmarkDotNet v0.15.8, Linux Arch Linux
AMD Ryzen 7 3700X 1.76GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.0, 42.42.42.42424), X64 RyuJIT x86-64-v3
  Job-CASMLP : .NET 10.0.0 (10.0.0, 42.42.42.42424), X64 RyuJIT x86-64-v3

MinIterationTime=1s  Server=True  MaxIterationCount=48  
MaxWarmupIterationCount=32  MinIterationCount=16  MinWarmupIterationCount=16  

```
| Method        | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|-------------- |----------:|----------:|----------:|----------:|------:|--------:|--------:|-------:|----------:|------------:|
| _FlameCsv     |  2.408 ms | 0.0508 ms | 0.1002 ms |  2.345 ms |  1.00 |    0.06 |       - |      - |     512 B |        1.00 |
| _Sep          |  4.316 ms | 0.0084 ms | 0.0075 ms |  4.318 ms |  1.80 |    0.07 |       - |      - |    5936 B |       11.59 |
| _Sylvan       |  4.796 ms | 0.1047 ms | 0.2066 ms |  4.663 ms |  2.00 |    0.12 |       - |      - |   42025 B |       82.08 |
| _CsvHelper    | 36.527 ms | 0.7224 ms | 1.3745 ms | 36.674 ms | 15.20 |    0.82 | 62.5000 |      - | 2789168 B |    5,447.59 |
| _RecordParser |  9.205 ms | 0.0696 ms | 0.0683 ms |  9.208 ms |  3.83 |    0.15 | 54.6875 | 7.8125 | 2268217 B |    4,430.11 |
