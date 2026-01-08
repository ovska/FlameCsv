---
uid: architecture
---

# Architecture

For those who are interested in the performance work that has gone into the library,
here is a brief overview of the architecture and design decisions behind FlameCsv.

## Reading

FlameCsv uses low-level SIMD-accelerated parsers tuned to the platform it is running on. There are discrete implementations
for ARM (NEON), AVX-512, AVX-2, and SSE/WASM2. On platforms without SIMD support, a scalar fallback parser is used.

Fields are parsed as packed `uint32` values:

- Bit 31 determines if the field is a newline (last field of the record)
- Bit 30 determines if the EOL is a CRLF-sequence (the next field must start one character later than usual)
- Bit 29 detemrines if the field has quotes
- Bit 28 determines if the field needs escaping
- Bits 0-27 contain the end index of the field within the current data buffer

Packing the field metadata into a single `uint32` allows for efficient processing of fields in tight loops,
allows vectorized stores with SIMD compression (AVX2 and AVX512), and allows calculating record bounds efficiently,
either with a narrowed movemask, or a simple `(int)f < 0` to check if a field is an EOL. Similarly when materialising
a field, `(int)(f << 2) < 0` can be used to check if the field needs its' quotes trimmed or value unescaped.

## Writing

A `IBufferWriter<T>` is used to format the values directly into the output buffer. The written buffer is then scanned
for special characters (commas, quotes, newlines), and escaped as necessary. This approach allows the common case
(no unescaping/quoting needed) to be very fast.

There are multiple special cases for fast paths:

- Formatting value types (such as numbers) when using the default options can omit quoting checks completely
- Formatting strings: quote/escape needs are checked _before_ copying the value to the buffer, and if needed,
  the value is escaped while copying to the output buffer
