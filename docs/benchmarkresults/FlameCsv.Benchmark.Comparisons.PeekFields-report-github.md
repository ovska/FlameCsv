```

BenchmarkDotNet v0.14.0, Windows 10 (10.0.19045.5608/22H2/2022Update)
AMD Ryzen 7 3700X, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.100
  [Host]     : .NET 9.0.0 (9.0.24.52809), X64 RyuJIT AVX2
  DefaultJob : .NET 9.0.0 (9.0.24.52809), X64 RyuJIT AVX2


```
| Method        | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Allocated | Alloc Ratio |
|-------------- |----------:|----------:|----------:|------:|--------:|---------:|---------:|----------:|------------:|
| _FlameCsv     |  3.292 ms | 0.0636 ms | 0.0595 ms |  1.00 |    0.02 |        - |        - |     322 B |        1.00 |
| _Sep          |  4.431 ms | 0.0624 ms | 0.0584 ms |  1.35 |    0.03 |        - |        - |    5942 B |       18.45 |
| _Sylvan       |  5.014 ms | 0.0748 ms | 0.0700 ms |  1.52 |    0.03 |        - |        - |   42029 B |      130.52 |
| _RecordParser |  6.358 ms | 0.0512 ms | 0.0479 ms |  1.93 |    0.04 | 320.3125 | 226.5625 | 2584418 B |    8,026.14 |
| _CsvHelper    | 34.877 ms | 0.1836 ms | 0.1533 ms | 10.60 |    0.19 | 333.3333 |        - | 2789195 B |    8,662.10 |
