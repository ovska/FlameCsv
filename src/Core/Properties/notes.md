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

| Method | Alt   |       Mean |   StdDev | Ratio |
| ------ | ----- | ---------: | -------: | ----: |
| V128   | False | 2,601.7 us | 18.92 us |  1.00 |
| V128   | True  |   906.3 us |  4.28 us |  1.00 |

### #### Unroll factor 3
| Method | Alt   |       Mean |  StdDev | Ratio |
| ------ | ----- | ---------: | ------: | ----: |
| V128   | False | 1,964.9 us | 4.60 us |  1.00 |
| V128   | True  |   798.9 us | 7.01 us |  1.00 |

#### Unroll factor 4
 | Method | Alt   |       Mean |   StdDev | Ratio |
 | ------ | ----- | ---------: | -------: | ----: |
 | V128   | False | 2,244.7 us | 10.90 us |  1.00 |
 | V128   | True  |   841.0 us |  4.33 us |  1.00 |

### Before and after with 256 bit vectors
| Method | Alt   |       Mean |  StdDev |
| ------ | ----- | ---------: | ------: |
| V256   | False | 1,725.7 us | 7.91 us |
| V256   | True  |   681.9 us | 3.44 us |

#### Unroll factor 4
| Method | Alt   |       Mean |  StdDev |
| ------ | ----- | ---------: | ------: |
| V256   | False | 1,543.9 us | 6.15 us |
| V256   | True  |   647.2 us | 4.39 us |

#### Unroll factor 5
| Method | Alt   |       Mean |  StdDev |
| ------ | ----- | ---------: | ------: |
| V256   | False | 1,518.5 us | 4.44 us |
| V256   | True  |   629.3 us | 2.73 us |
