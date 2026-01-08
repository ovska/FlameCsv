```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.2 (25C56) [Darwin 25.2.0]
Apple M4 Max, 1 CPU, 16 logical and 16 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.0, 10.0.25.52411), Arm64 RyuJIT armv8.0-a
  Job-CASMLP : .NET 10.0.0 (10.0.0, 10.0.25.52411), Arm64 RyuJIT armv8.0-a

MinIterationTime=1s  Server=True  MaxIterationCount=48  
MaxWarmupIterationCount=32  MinIterationCount=16  MinWarmupIterationCount=16  

```
| Method        | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0    | Gen1   | Gen2   | Allocated | Alloc Ratio |
|-------------- |----------:|----------:|----------:|------:|--------:|--------:|-------:|-------:|----------:|------------:|
| _FlameCsv     |  1.328 ms | 0.0089 ms | 0.0084 ms |  1.00 |    0.01 |       - |      - |      - |     512 B |        1.00 |
| _Sep          |  2.895 ms | 0.0165 ms | 0.0162 ms |  2.18 |    0.02 |       - |      - |      - |    5856 B |       11.44 |
| _Sylvan       |  8.358 ms | 0.0439 ms | 0.0389 ms |  6.29 |    0.05 |       - |      - |      - |   41410 B |       80.88 |
| _CsvHelper    | 19.826 ms | 0.0524 ms | 0.0515 ms | 14.93 |    0.10 | 15.6250 |      - |      - | 2789168 B |    5,447.59 |
| _RecordParser |  4.294 ms | 0.0176 ms | 0.0173 ms |  3.23 |    0.02 | 15.6250 | 3.9063 | 3.9063 | 2924842 B |    5,712.58 |
