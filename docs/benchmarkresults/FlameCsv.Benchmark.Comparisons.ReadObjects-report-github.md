```

BenchmarkDotNet v0.14.0, Windows 10 (10.0.19045.5608/22H2/2022Update)
AMD Ryzen 7 3700X, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.100
  [Host]     : .NET 9.0.0 (9.0.24.52809), X64 RyuJIT AVX2
  DefaultJob : .NET 9.0.0 (9.0.24.52809), X64 RyuJIT AVX2


```
| Method        | Mean     | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Gen2   | Allocated | Alloc Ratio |
|-------------- |---------:|----------:|----------:|------:|--------:|---------:|---------:|-------:|----------:|------------:|
| _Flame_SrcGen | 2.506 ms | 0.0472 ms | 0.0524 ms |  1.00 |    0.03 | 207.0313 |   3.9063 |      - |   1.66 MB |        1.00 |
| _FlameCsv     | 2.308 ms | 0.0387 ms | 0.0362 ms |  0.92 |    0.02 | 207.0313 |   3.9063 |      - |   1.66 MB |        1.00 |
| _Sylvan       | 2.570 ms | 0.0388 ms | 0.0363 ms |  1.03 |    0.03 | 328.1250 |  42.9688 |      - |   2.64 MB |        1.59 |
| _RecordParser | 4.673 ms | 0.0648 ms | 0.0607 ms |  1.87 |    0.04 | 242.1875 | 140.6250 |      - |   1.93 MB |        1.16 |
| _CsvHelper    | 6.424 ms | 0.0067 ms | 0.0056 ms |  2.56 |    0.05 | 437.5000 |  70.3125 | 7.8125 |   3.49 MB |        2.10 |
