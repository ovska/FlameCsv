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
The SIMD tokenizers also have separate JIT-constant folded paths for LF-newlines and options without quotes.

After tokenization, a second stage scans through the packed fields to store the record end positions.
When the next record is read, the previous record's start position is adjusted if it was a CRLF, avoiding extra
ops on every single field access to check for a CRLF offset.

### Architecture specific optimizations

- *AVX-512:* When there are no quotes, and there are 16 or less control characters (delimiters and newlines) per
  64 bytes, the matches can be compressed directly to the packed field buffer using `VPCOMPRESSB`, pseudocode:
        - AND the 0/0xFF match vector with an IOTA vector to get the match indices
        - OR this vector with the EOL-match vector with bit 7 set to tag the newline matches
        - Compress the resulting vector
        - Widen twice, sign extending to preserve EOL bits
        - Clean up the extended bits, save for bit 31 (and 30 if the matches were CRLFs)
        - Add the broadcast index to get the field end positions
        - Store to the output buffer
- *AVX2:* Similar to AVX-512, but uses LUT-shuffle based compress-emulation (from simdjson) when there are <=8 matches per 32 characters
- *ARM:* Uses zipped loads to to process 64 bytes at a time to do a fast movemask emulation (from aqrit),
  and heavily leverages ILP to process as many vector ops at a time as possible.

The generic SIMD tokenizer and ARM implementations also use unrolled writes to write up to 8 fields at a time to keep
the happy 90% case branch free. The EOL position can also be written this way, as a `tzcnt(maskLF)` just writes past the end
of the valid values when there is no newline in the current block.

## Writing

A `IBufferWriter<T>` is used to format the values directly into the output buffer. The written buffer is then scanned
for special characters (commas, quotes, newlines), and escaped as necessary. This approach allows the common case
(no unescaping/quoting needed) to be very fast.

There are a few fast path special cases:

- Formatting value types (such as numbers) when using the default options can omit quoting checks completely
- Formatting strings: quote/escape needs are checked *before* copying the value to the buffer, and if needed,
  the value is escaped while copying to the output buffer
