## ParseDelimiters popcount frequencies

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

### #### Unroll factor 3 ❌
| Method | Quoted |       Mean |  StdDev | Ratio |
| ------ | ------ | ---------: | ------: | ----: |
| V128   | False  | 1,964.9 us | 4.60 us |  1.00 |
| V128   | True   |   798.9 us | 7.01 us |  1.00 |

#### Unroll factor 4 ✔
 | Method | Quoted |       Mean |   StdDev | Ratio |
 | ------ | ------ | ---------: | -------: | ----: |
 | V128   | False  | 2,244.7 us | 10.90 us |  1.00 |
 | V128   | True   |   841.0 us |  4.33 us |  1.00 |

### Before and after with 256 bit vectors
| Method | Quoted |       Mean |  StdDev |
| ------ | ------ | ---------: | ------: |
| V256   | False  | 1,725.7 us | 7.91 us |
| V256   | True   |   681.9 us | 3.44 us |

#### Unroll factor 4 ❌
| Method | Quoted |       Mean |  StdDev |
| ------ | ------ | ---------: | ------: |
| V256   | False  | 1,543.9 us | 6.15 us |
| V256   | True   |   647.2 us | 4.39 us |

#### Unroll factor 5 ✔
| Method | Quoted |       Mean |  StdDev |
| ------ | ------ | ---------: | ------: |
| V256   | False  | 1,518.5 us | 4.44 us |
| V256   | True   |   629.3 us | 2.73 us |

#### goto instead of embedding ParseDelimiters ✔

| Method | Quoted |       Mean |  StdDev |
| ------ | ------ | ---------: | ------: |
| V256   | False  | 1,472.5 us | 6.04 us |
| V256   | True   |   621.7 us | 2.67 us |

#### Moving hasDelimiter movemask before check for maskAny ✔

| Method | Quoted |       Mean |  StdDev |
| ------ | ------ | ---------: | ------: |
| V256   | False  | 1,442.1 us | 1.39 us |
| V256   | True   |   612.4 us | 3.14 us |

#### Specialized AllBitsBefore impl ✔
| Method | Quoted |       Mean |  StdDev |
| ------ | ------ | ---------: | ------: |
| V256   | False  | 1,432.9 us | 7.25 us |
| V256   | True   |   610.2 us | 2.46 us |

#### Moving ParseDelimiters closer to the beginning of the loop ✔
| Method | Quoted |       Mean |  StdDev |
| ------ | ------ | ---------: | ------: |
| V256   | False  | 1,428.0 us | 6.02 us |
| V256   | True   |   608.3 us | 3.10 us |

#### Using an early-bail out for when there are unresolved quotes ✔
| Method | Quoted |       Mean |  StdDev |
| ------ | ------ | ---------: | ------: |
| V256   | False  | 1,425.3 us | 3.56 us |
| V256   | True   |   596.1 us | 1.34 us |

#### Omitting the TrySkipQuoted branch ❌
| Method | Quoted |       Mean |  StdDev |
| ------ | ------ | ---------: | ------: |
| V256   | False  | 1,466.6 us | 4.10 us |
| V256   | True   |   610.8 us | 2.97 us |

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

## Only unquoted happy case
| Method   | Quoted |     Mean |
| -------- | ------ | -------: |
| GetField | False  | 814.6 us |
| GetField | True   | 286.8 us |

## After slicing first field from Meta span at constructor
| Method   | Quoted |     Mean |
| -------- | ------ | -------: |
| GetField | False  | 745.4 us |
| GetField | True   | 269.9 us |

## Bitwise OR for trimming & special count
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

## Loading the address outright and adjusting it
| Method |     Mean |    Error |   StdDev | Ratio |
| ------ | -------: | -------: | -------: | ----: |
| TryPop | 64.30 us | 0.195 us | 0.239 us |  1.00 |
Code size: 216

## Using pointer arithmetic instead of manual position adjustment
| Method |     Mean |    Error |   StdDev | Ratio |
| ------ | -------: | -------: | -------: | ----: |
| TryPop | 70.87 us | 0.131 us | 0.166 us |  1.00 |

## Using nint for pos and end index
| Method |     Mean |    Error |   StdDev | Ratio |
| ------ | -------: | -------: | -------: | ----: |
| TryPop | 60.96 us | 0.156 us | 0.180 us |  1.00 |
Code size: 186

## Omitting found-bool and using goto directly
| Method |     Mean |    Error |   StdDev | Ratio |
| ------ | -------: | -------: | -------: | ----: |
| TryPop | 53.54 us | 0.106 us | 0.138 us |  1.00 |
Code size: 186

## Comparing directly with memory instead of loading bytes to variables/registers
| Method |     Mean |    Error |   StdDev | Ratio |
| ------ | -------: | -------: | -------: | ----: |
| TryPop | 53.63 us | 0.069 us | 0.084 us |  1.00 |
Code size: 175

### TODO:

Attempt and compare with full parsing routine (169 bytes, slightly different instructions, 10% slower in isolation):
```csharp
ref MetaSegment ms = ref Unsafe.As<ArraySegment<Meta>, MetaSegment>(ref Unsafe.AsRef(in fields));
ms.count = (int)pos + 1;
ms.offset = _index;
ms.array = _array;
```
