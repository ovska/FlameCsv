## ParseDelimiters popcount frequencies

Benchmarks done with LF.

### 256 bit vectors
| Count | 65K    | Sample |
| ----- | ------ | ------ |
| 1     | 20161  | 8814   |
| 2     | 29957  | 6534   |
| 3     | 46465  | 8482   |
| 4     | 104816 | 10145  |
| 5     | 33603  | 7989   |
| 6     | 134    | 519    |
| 7     | 0      | 0      |

### 128 bit vectors
| Count | 65K   | Sample |
| ----- | ----- | ------ |
| 1     | 14365 | 11137  |
| 2     | 9842  | 16613  |
| 3     | 5359  | 5359   |
| 4     | 62    | 63     |
| 5     | 0     | 0      |

### Before and after with 128 bit vectors

| Method | Quoted |       Mean |   StdDev | Ratio |
| ------ | ------ | ---------: | -------: | ----: |
| V128   | False  | 2,601.7 us | 18.92 us |  1.00 |
| V128   | True   |   906.3 us |  4.28 us |  1.00 |

### #### Unroll factor 3 👎
| Method | Quoted |       Mean |  StdDev | Ratio |
| ------ | ------ | ---------: | ------: | ----: |
| V128   | False  | 1,964.9 us | 4.60 us |  1.00 |
| V128   | True   |   798.9 us | 7.01 us |  1.00 |

#### Unroll factor 4 👍
 | Method | Quoted |       Mean |   StdDev | Ratio |
 | ------ | ------ | ---------: | -------: | ----: |
 | V128   | False  | 2,244.7 us | 10.90 us |  1.00 |
 | V128   | True   |   841.0 us |  4.33 us |  1.00 |

### Before and after with 256 bit vectors
| Method | Quoted |       Mean |  StdDev |
| ------ | ------ | ---------: | ------: |
| V256   | False  | 1,725.7 us | 7.91 us |
| V256   | True   |   681.9 us | 3.44 us |

#### Unroll factor 4 👎
| Method | Quoted |       Mean |  StdDev |
| ------ | ------ | ---------: | ------: |
| V256   | False  | 1,543.9 us | 6.15 us |
| V256   | True   |   647.2 us | 4.39 us |

#### Unroll factor 5 👍
| Method | Quoted |       Mean |  StdDev |
| ------ | ------ | ---------: | ------: |
| V256   | False  | 1,518.5 us | 4.44 us |
| V256   | True   |   629.3 us | 2.73 us |

#### goto instead of embedding ParseDelimiters 👍

| Method | Quoted |       Mean |  StdDev |
| ------ | ------ | ---------: | ------: |
| V256   | False  | 1,472.5 us | 6.04 us |
| V256   | True   |   621.7 us | 2.67 us |

#### Moving hasDelimiter movemask before check for maskAny 👍

| Method | Quoted |       Mean |  StdDev |
| ------ | ------ | ---------: | ------: |
| V256   | False  | 1,442.1 us | 1.39 us |
| V256   | True   |   612.4 us | 3.14 us |

#### Specialized AllBitsBefore impl 👍
| Method | Quoted |       Mean |  StdDev |
| ------ | ------ | ---------: | ------: |
| V256   | False  | 1,432.9 us | 7.25 us |
| V256   | True   |   610.2 us | 2.46 us |

#### Moving ParseDelimiters closer to the beginning of the loop 👍
| Method | Quoted |       Mean |  StdDev |
| ------ | ------ | ---------: | ------: |
| V256   | False  | 1,428.0 us | 6.02 us |
| V256   | True   |   608.3 us | 3.10 us |

#### Using an early-bail out for when there are unresolved quotes 👍
| Method | Quoted |       Mean |  StdDev |
| ------ | ------ | ---------: | ------: |
| V256   | False  | 1,425.3 us | 3.56 us |
| V256   | True   |   596.1 us | 1.34 us |

#### Omitting the TrySkipQuoted branch 👎
| Method | Quoted |       Mean |  StdDev |
| ------ | ------ | ---------: | ------: |
| V256   | False  | 1,466.6 us | 4.10 us |
| V256   | True   |   610.8 us | 2.97 us |

### Storing newline meta indexes experiment

Data seeking was 650 us and 1.490 us. Might be worth exploring further.

FindNewlineBench:
| Method      |      Mean |     Error |    StdDev | Ratio |
| ----------- | --------: | --------: | --------: | ----: |
| FromIndices |  5.046 us | 0.0191 us | 0.0249 us |  0.09 |
| TryPop      | 56.664 us | 0.0503 us | 0.0599 us |  1.00 |

After optimizing the newline storing
| Method | Quoted |       Mean |       Mean |
| ------ | ------ | ---------: | ---------: |
| V256   | False  | 1,464.6 us | 1,465.0 us |
| V256   | True   |   612.6 us |   618.3 us |

Note: don't store EOL in meta if going this route, should give a ~1% perf boost when tokenizing.

No eol storage 👎
| Method | Quoted |       Mean |       Mean |
| ------ | ------ | ---------: | ---------: |
| V256   | False  | 1,475.5 us | 1,471.1 us |
| V256   | True   |   618.6 us |   619.9 us |

Storing both currentMeta and EOLs in the same ref struct 👎
| Method | Quoted |       Mean |  StdDev |
| ------ | ------ | ---------: | ------: |
| V256   | False  | 1,569.6 us | 6.71 us |
| V256   | True   |   659.1 us | 2.52 us |

Comparing with the old method
| Method |     Mean |     Error |    StdDev | Ratio |
| ------ | -------: | --------: | --------: | ----: |
| Old    | 1.706 ms | 0.0050 ms | 0.0064 ms |  1.00 |
| New    | 1.596 ms | 0.0036 ms | 0.0047 ms |  0.94 |

Other benefits:
- Record count known up-front
- No need to iterate fields to find record bounds

## Notes

- `uint` instead of `nuint` for masks seems to just produce a ~dozen extra `mov`s in the Tokenize-method
- Aligned vector reads aren't worth it since I/O is such a small part of the equation; the code complexity and extra path to align the data isn't worth it
- Prefetching the vector gives a massive 5-10% speedup in an otherwise well-optimized method

## To-do

- Profile: whether a separate `LF` parsing path is worth it, as `CRLF` config can read `LF` files just fine, just a tiny bit slower
- Profile: Reverse movemask bits and use `LZCNT` on ARM (a `ClearNextBit(ref nuint mask)` method inlines nicely on x86 with identical ASM)

# GetField

## Original
| Method   | Quoted |       Mean |
| -------- | ------ | ---------: |
| GetField | False  | 1,077.4 us |
| GetField | True   |   271.0 us |

## Only unquoted happy case 👍
| Method   | Quoted |     Mean |
| -------- | ------ | -------: |
| GetField | False  | 814.6 us |
| GetField | True   | 286.8 us |

## After slicing first field from Meta span at constructor 👍
| Method   | Quoted |     Mean |
| -------- | ------ | -------: |
| GetField | False  | 745.4 us |
| GetField | True   | 269.9 us |

## Bitwise OR for trimming & special count 👍
| Method   | Quoted |     Mean |
| -------- | ------ | -------: |
| GetField | False  | 712.6 us |
| GetField | True   | 248.8 us |

# TryPop in MetaBuffer

## Original
| Method |     Mean |    Error |   StdDev | Ratio |
| ------ | -------: | -------: | -------: | ----: |
| TryPop | 77.61 us | 0.167 us | 0.199 us |  1.00 |
Code size: 248

## Loading the address outright and adjusting it 👍
| Method |     Mean |    Error |   StdDev | Ratio |
| ------ | -------: | -------: | -------: | ----: |
| TryPop | 64.30 us | 0.195 us | 0.239 us |  1.00 |
Code size: 216 👍

## Using pointer arithmetic instead of manual position adjustment 👎
| Method |     Mean |    Error |   StdDev | Ratio |
| ------ | -------: | -------: | -------: | ----: |
| TryPop | 70.87 us | 0.131 us | 0.166 us |  1.00 |

## Using nint for pos and end index 👍
| Method |     Mean |    Error |   StdDev | Ratio |
| ------ | -------: | -------: | -------: | ----: |
| TryPop | 60.96 us | 0.156 us | 0.180 us |  1.00 |
Code size: 186 👍

## Omitting found-bool and using goto directly 👍
| Method |     Mean |    Error |   StdDev | Ratio |
| ------ | -------: | -------: | -------: | ----: |
| TryPop | 53.54 us | 0.106 us | 0.138 us |  1.00 |
Code size: 186 🤏

## Comparing directly with memory instead of loading bytes to variables/registers 🤏
| Method |     Mean |    Error |   StdDev | Ratio |
| ------ | -------: | -------: | -------: | ----: |
| TryPop | 53.63 us | 0.069 us | 0.084 us |  1.00 |
Code size: 175 👍

### Tests on newline bit packing

| CRLF  | Quoted |       Mean |   StdDev |
| ----- | ------ | ---------: | -------: |
| False | False  | 1,466.7 us |  3.56 us |
| False | True   |   614.7 us |  1.59 us |
| True  | False  | 1,553.1 us | 13.37 us |
| True  | True   |   640.1 us |  3.90 us |



### Passing delimiter to IsNewline ❓❔
| CRLF  | Quoted |       Mean |  StdDev |
| ----- | ------ | ---------: | ------: |
| False | False  | 1,490.2 us | 5.50 us |
| False | True   |   375.4 us | 1.69 us |
| True  | False  | 1,572.2 us | 5.89 us |
| True  | True   |   397.2 us | 2.01 us |


### Short circuit on popcount 1 in parseline ends
| CRLF  | Quoted |       Mean |  StdDev |
| ----- | ------ | ---------: | ------: |
| False | False  | 1,481.6 us | 3.58 us |
| False | True   |   369.8 us | 1.97 us |
| True  | False  | 1,587.2 us | 8.23 us |
| True  | True   |   404.0 us | 1.83 us |


### Calculating offset from first in start of method
| CRLF  | Quoted |       Mean |  StdDev |
| ----- | ------ | ---------: | ------: |
| False | False  | 1,492.3 us | 6.78 us |
| False | True   |   375.5 us | 1.49 us |
| True  | False  | 1,573.0 us | 6.79 us |
| True  | True   |   394.5 us | 1.92 us |


### Using bitwise tricks for quoteCount and isQuote
| CRLF  | Quoted |     Mean |  StdDev | Ratio |
| ----- | ------ | -------: | ------: | ----: |
| False | True   | 371.0 us | 1.25 us |  1.00 |
| True  | True   | 384.3 us | 1.79 us |  1.00 |

### After all optimizations
| Newline      | Quoted | Chars |       Mean |  StdDev |
| ------------ | ------ | ----- | ---------: | ------: |
| LF           | False  | False | 1,488.4 us | 9.26 us |
| LF_With_CRLF | False  | False | 1,590.6 us | 6.33 us |
| CRLF         | False  | False | 1,578.1 us | 6.65 us |
| LF           | False  | True  | 1,650.4 us | 6.20 us |
| LF_With_CRLF | False  | True  | 1,722.2 us | 6.12 us |
| CRLF         | False  | True  | 1,729.1 us | 6.38 us |
| LF           | True   | False |   376.2 us | 2.23 us |
| LF_With_CRLF | True   | False |   388.7 us | 2.52 us |
| CRLF         | True   | False |   392.2 us | 3.13 us |
| LF           | True   | True  |   400.9 us | 1.91 us |
| LF_With_CRLF | True   | True  |   408.0 us | 3.53 us |
| CRLF         | True   | True  |   409.4 us | 3.36 us |

### Eager vector loading
| Method | Newline      | Quoted | Chars |       Mean |   StdDev | Ratio |
| ------ | ------------ | ------ | ----- | ---------: | -------: | ----: |
| V256   | LF           | False  | False | 1,483.3 us |  9.56 us |  1.00 |
| V256   | LF_With_CRLF | False  | False | 1,578.5 us |  8.29 us |  1.00 |
| V256   | CRLF         | False  | False | 1,554.7 us |  8.70 us |  1.00 |
| V256   | LF           | False  | True  | 1,608.0 us |  7.76 us |  1.00 |
| V256   | LF_With_CRLF | False  | True  | 1,695.8 us |  7.91 us |  1.00 |
| V256   | CRLF         | False  | True  | 1,706.9 us | 16.99 us |  1.00 |
| V256   | LF           | True   | False |   368.2 us |  3.77 us |  1.00 |
| V256   | LF_With_CRLF | True   | False |   378.1 us |  2.79 us |  1.00 |
| V256   | CRLF         | True   | False |   394.3 us |  4.35 us |  1.00 |
| V256   | LF           | True   | True  |   389.6 us |  2.59 us |  1.00 |
| V256   | LF_With_CRLF | True   | True  |   410.9 us |  2.62 us |  1.00 |
| V256   | CRLF         | True   | True  |   407.2 us |  2.26 us |  1.00 |

## Swapping quotecount and newline zero checks
| Newline | Quoted | Chars |     Mean |  StdDev |
| ------- | ------ | ----- | -------: | ------: |
| LF      | False  | False | 355.8 us | 2.27 us |
| CRLF    | False  | False | 389.0 us | 2.29 us |

## PopCount 1 optimization for newlines
| Quoted | Newline |       Mean |  StdDev |
| ------ | ------- | ---------: | ------: |
| False  | LF      | 1,456.9 us | 4.27 us |
| False  | CRLF    | 1,554.4 us | 3.49 us |
| True   | LF      |   370.9 us | 1.43 us |
| True   | CRLF    |   386.1 us | 2.35 us |

## Use a branch for record end offset writing
| Method | Quoted | Newline |       Mean |  StdDev | Ratio |
| ------ | ------ | ------- | ---------: | ------: | ----: |
| V256   | False  | CRLF    | 1,512.3 us | 7.59 us |  1.00 |
| V256   | True   | CRLF    |   366.2 us | 2.70 us |  1.00 |

## Use a branch for newline check in ParseDelimitersAndLineEnds
| Method | Quoted | Newline |       Mean |  StdDev | Ratio |
| ------ | ------ | ------- | ---------: | ------: | ----: |
| V256   | False  | LF      | 1,403.1 us | 2.86 us |  1.00 |
| V256   | False  | CRLF    | 1,487.5 us | 5.20 us |  1.00 |
| V256   | True   | LF      |   357.0 us | 0.51 us |  1.00 |
| V256   | True   | CRLF    |   355.0 us | 2.68 us |  1.00 |


## nuint vs uint masks

Before:
| Quoted | Newline |       Mean |  StdDev |
| ------ | ------- | ---------: | ------: |
| False  | LF      | 1,430.9 us | 7.83 us |
| False  | CRLF    | 1,483.6 us | 4.61 us |
| True   | LF      |   370.8 us | 3.77 us |
| True   | CRLF    |   350.1 us | 1.88 us |

After:
| Quoted | Newline |       Mean |  StdDev |
| ------ | ------- | ---------: | ------: |
| False  | LF      | 1,381.0 us | 6.90 us |
| False  | CRLF    | 1,461.9 us | 6.59 us |
| True   | LF      |   364.3 us | 4.07 us |
| True   | CRLF    |   345.1 us | 3.45 us |

## Precompute delimiter offset

Before:
| Method | Newline |     Mean |    StdDev |
| ------ | ------- | -------: | --------: |
| V128   | LF      | 1.380 ms | 0.0017 ms |
| V128   | CRLF    | 1.455 ms | 0.0035 ms |

After:
| Method | Newline |     Mean |    StdDev |
| ------ | ------- | -------: | --------: |
| V128   | LF      | 1.354 ms | 0.0039 ms |
| V128   | CRLF    | 1.397 ms | 0.0048 ms |

## Scalar parser

| Method | Chars | Newline |      Mean |    StdDev | Ratio |
| ------ | ----- | ------- | --------: | --------: | ----: |
| Old    | False | LF      |  8.644 ms | 0.0405 ms |  1.00 |
| LUT    | False | LF      |  6.473 ms | 0.0490 ms |  0.75 |
|        |       |         |           |           |       |
| Old    | False | CRLF    | 10.831 ms | 0.0169 ms |  1.00 |
| LUT    | False | CRLF    |  6.401 ms | 0.0330 ms |  0.59 |
|        |       |         |           |           |       |
| Old    | True  | LF      |  8.384 ms | 0.0158 ms |  1.00 |
| LUT    | True  | LF      |  7.219 ms | 0.0363 ms |  0.86 |
|        |       |         |           |           |       |
| Old    | True  | CRLF    |  9.669 ms | 0.0334 ms |  1.00 |
| LUT    | True  | CRLF    |  7.315 ms | 0.0778 ms |  0.76 |

## Using AVX512VBMI2.VL.Compress for delimiters
~~Before: 851.7 and 928.7~~
~~After: 821.4 and 909.0~~

Discrete AVX512 tokenizer implemented.

## Prefetching

| Prefetch Distance | UTF8 (Chars=False) | UTF16 (Chars=True) |
| ----------------: | -----------------: | -----------------: |
|              None |           1.310 ms |           1.482 ms |
|               128 |           1.333 ms |           1.464 ms |
|               256 |           1.333 ms |           1.445 ms |
|               384 |           1.338 ms |           1.433 ms |
|               512 |           1.328 ms |           1.444 ms |

## Refactored to bit-packed end indexes + separate quotes

| Method | Chars | Quoted | Newline |       Mean |  StdDev | Ratio |
| ------ | ----- | ------ | ------- | ---------: | ------: | ----: |
| V128   | False | False  | LF      | 1,291.1 us | 4.85 us |  1.00 |
| Avx2   | False | False  | LF      | 1,031.1 us | 4.21 us |  0.80 |
|        |       |        |         |            |         |       |
| V128   | False | False  | CRLF    | 1,370.9 us | 5.90 us |  1.00 |
| Avx2   | False | False  | CRLF    | 1,365.4 us | 4.03 us |  0.99 |
|        |       |        |         |            |         |       |
| V128   | False | True   | LF      |   540.7 us | 3.62 us |  1.00 |
| Avx2   | False | True   | LF      |   328.5 us | 1.00 us |  0.61 |
|        |       |        |         |            |         |       |
| V128   | False | True   | CRLF    |   602.2 us | 2.27 us |  1.00 |
| Avx2   | False | True   | CRLF    |   437.2 us | 2.05 us |  0.73 |
|        |       |        |         |            |         |       |
| V128   | True  | False  | LF      | 1,398.1 us | 5.30 us |  1.00 |
| Avx2   | True  | False  | LF      | 1,147.2 us | 4.25 us |  0.82 |
|        |       |        |         |            |         |       |
| V128   | True  | False  | CRLF    | 1,526.0 us | 5.82 us |  1.00 |
| Avx2   | True  | False  | CRLF    | 1,468.0 us | 4.52 us |  0.96 |
|        |       |        |         |            |         |       |
| V128   | True  | True   | LF      |   555.1 us | 2.50 us |  1.00 |
| Avx2   | True  | True   | LF      |   358.8 us | 1.41 us |  0.65 |
|        |       |        |         |            |         |       |
| V128   | True  | True   | CRLF    |   597.9 us | 2.99 us |  1.00 |
| Avx2   | True  | True   | CRLF    |   458.9 us | 1.69 us |  0.77 |

## Elide LUT bounds checks (AVX2)

| Method | Newline |       Mean |    StdDev | Ratio |
| ------ | ------- | ---------: | --------: | ----: |
| V128   | LF      | 1,284.4 us |   2.95 us |  1.00 |
| Avx2   | LF      |   998.0 us |   3.04 us |  0.78 |
| V128   | CRLF    |   1.373 ms | 0.0183 ms |  1.00 |
| Avx2   | CRLF    |   1.322 ms | 0.0035 ms |  0.96 |

## Eliminate branch in CRLF fixup vector (AVX2)

| Method | Newline |     Mean |    StdDev | Ratio |
| ------ | ------- | -------: | --------: | ----: |
| V128   | CRLF    | 1.352 ms | 0.0050 ms |  1.00 |
| Avx2   | CRLF    | 1.247 ms | 0.0050 ms |  0.92 |

## After adjustments (AVX2)

| Method | Chars | Quoted | Newline      |       Mean |   StdDev | Ratio |
| ------ | ----- | ------ | ------------ | ---------: | -------: | ----: |
| V128   | False | False  | LF           | 1,280.2 us |  4.99 us |  1.00 |
| Avx2   | False | False  | LF           |   987.1 us |  4.37 us |  0.77 |
|        |       |        |              |            |          |       |
| V128   | False | False  | LF_With_CRLF | 1,328.6 us |  3.05 us |  1.00 |
| Avx2   | False | False  | LF_With_CRLF | 1,217.6 us |  3.55 us |  0.92 |
|        |       |        |              |            |          |       |
| V128   | False | False  | CRLF         | 1,340.1 us |  2.75 us |  1.00 |
| Avx2   | False | False  | CRLF         | 1,221.1 us |  3.90 us |  0.91 |
|        |       |        |              |            |          |       |
| V128   | False | True   | LF           |   537.2 us |  1.92 us |  1.00 |
| Avx2   | False | True   | LF           |   329.2 us |  0.80 us |  0.61 |
|        |       |        |              |            |          |       |
| V128   | False | True   | LF_With_CRLF |   570.0 us |  2.00 us |  1.00 |
| Avx2   | False | True   | LF_With_CRLF |   381.5 us |  1.30 us |  0.67 |
|        |       |        |              |            |          |       |
| V128   | False | True   | CRLF         |   608.7 us | 12.46 us |  1.00 |
| Avx2   | False | True   | CRLF         |   416.2 us |  1.82 us |  0.68 |
|        |       |        |              |            |          |       |
| V128   | True  | False  | LF           | 1,387.3 us |  7.46 us |  1.00 |
| Avx2   | True  | False  | LF           | 1,078.4 us |  5.21 us |  0.78 |
|        |       |        |              |            |          |       |
| V128   | True  | False  | LF_With_CRLF | 1,485.3 us |  4.27 us |  1.00 |
| Avx2   | True  | False  | LF_With_CRLF | 1,329.8 us |  3.03 us |  0.90 |
|        |       |        |              |            |          |       |
| V128   | True  | False  | CRLF         | 1,514.4 us |  2.54 us |  1.00 |
| Avx2   | True  | False  | CRLF         | 1,337.7 us |  5.06 us |  0.88 |
|        |       |        |              |            |          |       |
| V128   | True  | True   | LF           |   540.5 us |  1.37 us |  1.00 |
| Avx2   | True  | True   | LF           |   354.7 us |  1.07 us |  0.66 |
|        |       |        |              |            |          |       |
| V128   | True  | True   | LF_With_CRLF |   596.0 us |  2.32 us |  1.00 |
| Avx2   | True  | True   | LF_With_CRLF |   410.5 us |  0.58 us |  0.69 |
|        |       |        |              |            |          |       |
| V128   | True  | True   | CRLF         |   599.4 us |  2.92 us |  1.00 |
| Avx2   | True  | True   | CRLF         |   433.4 us |  1.73 us |  0.72 |

## More adjustments (AVX2)

| Chars | Quoted | Newline      |        Old |        New | Ratio |
| ----- | ------ | ------------ | ---------: | ---------: | ----: |
| False | False  | LF           | 1,294.8 us |   948.5 us |  0.73 |
| False | False  | LF_With_CRLF | 1,336.9 us | 1,238.0 us |  0.93 |
| False | False  | CRLF         | 1,362.7 us | 1,245.9 us |  0.91 |
| False | True   | LF           |   535.0 us |   372.4 us |  0.70 |
| False | True   | LF_With_CRLF |   578.3 us |   439.1 us |  0.76 |
| False | True   | CRLF         |   603.0 us |   432.4 us |  0.72 |
| True  | False  | LF           | 1,396.3 us | 1,121.1 us |  0.80 |
| True  | False  | LF_With_CRLF | 1,489.6 us | 1,423.2 us |  0.96 |
| True  | False  | CRLF         | 1,523.0 us | 1,432.7 us |  0.94 |
| True  | True   | LF           |   554.9 us |   379.8 us |  0.68 |
| True  | True   | LF_With_CRLF |   612.5 us |   458.4 us |  0.75 |
| True  | True   | CRLF         |   610.5 us |   454.9 us |  0.75 |

## Getting field end index

| Method      |     Mean |    StdDev | Ratio |     Diff |
| ----------- | -------: | --------: | ----: | -------: |
| No-Op       | 1.790 us | 0.0016 us |  1.00 | 0.000 us |
| No Sentinel | 3.128 us | 0.0017 us |  1.75 | 1.338 us |
| Sentinel    | 5.605 us | 0.0175 us |  3.13 | 3.815 us |

8000 fields.

Consider moving away from "StartOrEnd" sentinel bits for every single field, and use a branch instead.

## Field enumeration

| Method                    |      Mean |
| ------------------------- | --------: |
| Naive                     | ~1.700 ms |
| Precalculated EOL         |  1.622 ms |
| Less vector compares      |  1.606 ms |
| Aligned Reads             |  1.603 ms |
| Branch on zero vector     |  1.603 ms |
| Prefetch next             |  1.599 ms |
| Separate idx vs _eolIndex |  1.575 ms |
| Simplify end index        |  1.570 ms |
| Final working             |  1.573 ms |

## Generic SIMD prefix XOR for quotes

Before:
| Method | Chars | Quoted | Newline |     Mean |  StdDev | Ratio |
| ------ | ----- | ------ | ------- | -------: | ------: | ----: |
| V128   | False | True   | LF      | 527.0 us | 2.10 us |  1.00 |
| V128   | False | True   | CRLF    | 564.7 us | 2.02 us |  1.00 |

Naive prefix XOR:
| Method | Chars | Quoted | Newline |     Mean |  StdDev | Ratio |
| ------ | ----- | ------ | ------- | -------: | ------: | ----: |
| V128   | False | True   | LF      | 522.9 us | 1.40 us |  1.00 |
| V128   | False | True   | CRLF    | 571.3 us | 1.49 us |  1.00 |

| Method | Chars | Quoted | Newline |     Mean |  StdDev | Ratio |
| ------ | ----- | ------ | ------- | -------: | ------: | ----: |
| V128   | False | True   | LF      | 523.1 us | 2.02 us |  1.00 |
| V128   | False | True   | CRLF    | 591.0 us | 3.38 us |  1.00 |

Final
| Method | Chars | Quoted | Newline |     Mean |  StdDev | Ratio |
| ------ | ----- | ------ | ------- | -------: | ------: | ----: |
| V128   | False | True   | LF      | 493.5 us | 1.77 us |  1.00 |
| Avx2   | False | True   | LF      | 340.9 us | 1.23 us |  0.69 |
|        |       |        |         |          |         |       |
| V128   | False | True   | CRLF    | 569.8 us | 2.02 us |  1.00 |
| Avx2   | False | True   | CRLF    | 422.1 us | 1.22 us |  0.74 |

Remove bits before/after checks
| Method | Chars | Quoted | Newline |     Mean |  StdDev | Ratio |
| ------ | ----- | ------ | ------- | -------: | ------: | ----: |
| V128   | False | True   | LF      | 369.9 us | 1.66 us |  1.00 |
| Avx2   | False | True   | LF      | 353.9 us | 2.34 us |  0.96 |
|        |       |        |         |          |         |       |
| V128   | False | True   | CRLF    | 404.5 us | 1.16 us |  1.00 |
| Avx2   | False | True   | CRLF    | 424.8 us | 0.89 us |  1.05 |

| Method | Chars | Quoted | Newline |       Mean |  StdDev | Ratio |
| ------ | ----- | ------ | ------- | ---------: | ------: | ----: |
| V128   | False | False  | LF      | 1,430.9 us | 6.21 us |  1.00 |
| V128   | False | False  | CRLF    | 1,533.6 us | 5.59 us |  1.00 |
| V128   | False | True   | LF      |   384.5 us | 1.90 us |  1.00 |
| V128   | False | True   | CRLF    |   437.8 us | 1.69 us |  1.00 |

Refactored Generic:
| Chars | Quoted | Newline      |    Generic |       Avx2 | Ratio |  Faster |
| ----- | ------ | ------------ | ---------: | ---------: | ----: | ------: |
| False | False  | LF           | 1,404.6 us |   958.0 us |  0.68 |    AVX2 |
| False | False  | LF_With_CRLF | 1,464.2 us | 1,260.7 us |  0.86 |    AVX2 |
| False | False  | CRLF         | 1,464.9 us | 1,269.6 us |  0.87 |    AVX2 |
| False | True   | LF           |   364.6 us |   394.9 us |  1.08 | Generic |
| False | True   | LF_With_CRLF |   392.6 us |   436.5 us |  1.11 | Generic |
| False | True   | CRLF         |   414.2 us |   455.3 us |  1.10 | Generic |
| True  | False  | LF           | 1,531.3 us | 1,134.5 us |  0.74 |    AVX2 |
| True  | False  | LF_With_CRLF | 1,611.3 us | 1,442.5 us |  0.90 |    AVX2 |
| True  | False  | CRLF         | 1,622.9 us | 1,456.5 us |  0.90 |    AVX2 |
| True  | True   | LF           |   414.2 us |   416.3 us |  1.01 | Generic |
| True  | True   | LF_With_CRLF |   445.5 us |   481.6 us |  1.08 | Generic |
| True  | True   | CRLF         |   447.6 us |   451.0 us |  1.01 | Generic |

Generic is now faster in all quoted paths, which is very odd.

## Unroll delim/LF checks on LF (generic)

| Method | Chars | Quoted | Newline |       Mean |  StdDev | Ratio |
| ------ | ----- | ------ | ------- | ---------: | ------: | ----: |
| V128   | False | False  | LF      | 1,119.7 us | 2.71 us |  1.00 |
| V128   | False | True   | LF      |   333.6 us | 2.31 us |  1.00 |

## Supercharged generic

| Method | Chars | Quoted | Newline |       Mean |  StdDev | Ratio |
| ------ | ----- | ------ | ------- | ---------: | ------: | ----: |
| V128   | False | False  | LF      | 1,117.7 us | 3.31 us |  1.00 |
| V128   | False | False  | CRLF    | 1,368.2 us | 2.86 us |  1.00 |
| V128   | False | True   | LF      |   341.0 us | 1.05 us |  1.00 |
| V128   | False | True   | CRLF    |   407.5 us | 1.37 us |  1.00 |

## MSB shifting maskLF trick (AVX2)

| Method | Chars | Quoted | Newline |     Mean |  StdDev |
| ------ | ----- | ------ | ------- | -------: | ------: |
| Avx2   | False | True   | LF      | 389.9 us | 0.63 us |
| Avx2   | False | True   | CRLF    | 432.6 us | 0.87 us |

| Method | Chars | Quoted | Newline |     Mean |  StdDev | Ratio |
| ------ | ----- | ------ | ------- | -------: | ------: | ----: |
| V128   | False | True   | LF      | 338.0 us | 1.44 us |  1.00 |
| Avx2   | False | True   | LF      | 392.3 us | 1.04 us |  1.16 |
|        |       |        |         |          |         |       |
| V128   | False | True   | CRLF    | 446.6 us | 1.45 us |  1.00 |
| Avx2   | False | True   | CRLF    | 475.9 us | 1.59 us |  1.07 |

## Merge LF and delim checks for lfcount 0..1

| Method | Chars | Quoted | Newline |       Mean |  StdDev | Ratio |
| ------ | ----- | ------ | ------- | ---------: | ------: | ----: |
| V128   | False | False  | LF      | 1,011.4 us | 1.43 us |  1.00 |
| V128   | False | True   | LF      |   421.5 us | 1.24 us |  1.00 |

## After correctness refactor
| Method | Chars | Quoted | Newline      |       Mean |  StdDev | Ratio |
| ------ | ----- | ------ | ------------ | ---------: | ------: | ----: |
| V128   | False | False  | LF           | 1,031.9 us | 4.00 us |  1.00 |
| Avx2   | False | False  | LF           |   909.7 us | 3.85 us |  0.88 |
|        |       |        |              |            |         |       |
| V128   | False | False  | LF_With_CRLF | 1,347.0 us | 5.65 us |  1.00 |
| Avx2   | False | False  | LF_With_CRLF | 1,266.5 us | 7.42 us |  0.94 |
|        |       |        |              |            |         |       |
| V128   | False | False  | CRLF         | 1,360.0 us | 4.83 us |  1.00 |
| Avx2   | False | False  | CRLF         | 1,276.0 us | 4.58 us |  0.94 |
|        |       |        |              |            |         |       |
| V128   | False | True   | LF           |   425.1 us | 1.46 us |  1.00 |
| Avx2   | False | True   | LF           |   394.7 us | 1.56 us |  0.93 |
|        |       |        |              |            |         |       |
| V128   | False | True   | LF_With_CRLF |   516.7 us | 1.71 us |  1.00 |
| Avx2   | False | True   | LF_With_CRLF |   502.7 us | 1.60 us |  0.97 |
|        |       |        |              |            |         |       |
| V128   | False | True   | CRLF         |   516.8 us | 1.00 us |  1.00 |
| Avx2   | False | True   | CRLF         |   505.5 us | 1.16 us |  0.98 |
|        |       |        |              |            |         |       |
| V128   | True  | False  | LF           | 1,205.8 us | 4.43 us |  1.00 |
| Avx2   | True  | False  | LF           | 1,105.5 us | 3.62 us |  0.92 |
|        |       |        |              |            |         |       |
| V128   | True  | False  | LF_With_CRLF | 1,476.5 us | 5.57 us |  1.00 |
| Avx2   | True  | False  | LF_With_CRLF | 1,415.0 us | 6.55 us |  0.96 |
|        |       |        |              |            |         |       |
| V128   | True  | False  | CRLF         | 1,490.6 us | 4.79 us |  1.00 |
| Avx2   | True  | False  | CRLF         | 1,408.0 us | 1.96 us |  0.94 |
|        |       |        |              |            |         |       |
| V128   | True  | True   | LF           |   442.5 us | 0.38 us |  1.00 |
| Avx2   | True  | True   | LF           |   434.5 us | 1.63 us |  0.98 |
|        |       |        |              |            |         |       |
| V128   | True  | True   | LF_With_CRLF |   529.1 us | 0.60 us |  1.00 |
| Avx2   | True  | True   | LF_With_CRLF |   523.5 us | 1.35 us |  0.99 |
|        |       |        |              |            |         |       |
| V128   | True  | True   | CRLF         |   524.5 us | 1.40 us |  1.00 |
| Avx2   | True  | True   | CRLF         |   514.5 us | 1.88 us |  0.98 |

## Avoid a shuffle in AVX2 path for upper lane

915.31 us to 890.4 us. Use blend and offset ShuffleCombine.

## Alignment in AVX2 Tokenizer

Aligned
| Method |       Mean |  StdDev | Ratio | Code Size |
| ------ | ---------: | ------: | ----: | --------: |
| Bytes  |   870.7 us | 1.14 us |  1.00 |   1,113 B |
| Chars  | 1,084.0 us | 2.71 us |  1.24 |   1,225 B |

Unaligned
| Method |       Mean |  StdDev | Ratio | Code Size |
| ------ | ---------: | ------: | ----: | --------: |
| Bytes  |   872.3 us | 3.13 us |  1.00 |     995 B |
| Chars  | 1,087.9 us | 5.56 us |  1.25 |   1,055 B |

# Frequencies

```
Delim + Line ends
Total: 1104
1   0
2   0
3   302
4   731

Delimiters
Total: 16861
1   1455
2   2175
3   3374
4   7476
5   2370
6   11

Only line ends
Total: 3575
1   3575

-----------------------------

Any
Total 4018
1   32
2   384
3   971
4   1572
5   705
6   222
7   103
8   25
9   4

AnyNoQuotes
Total 65
1   0
2   0
3   5
4   39
5   21

Delimiters
Total: 14181
1   3038
2   2286
3   2928
4   3298
5   2502
6   128
7   1

Delimiters and line ends
Total: 3317
1   0
2   0
3   1844
4   1151
5   248
6   74

Line ends
Total: 1680
1   1680


-----------------------------

Quoted Data:
total iters     23400
delim/lf total  4999
delim before lf 915
delim after lf  765
mixed           3317

Unquoted Data:
total iters     18290
delim/lf total  4679
delim before lf 2205
delim after lf  1078
mixed           1104
```
