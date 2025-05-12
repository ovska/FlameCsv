---
uid: architecture
---

# Architecture

## Foreword

This document explains the inner workings and design philosophy of FlameCsv.
While not required for using the library, it provides insights into the internals for those interested.

> [!WARNING]
> Nerdy stuff ahead 🤓


## Reading

FlameCsv's reading implementation is built on two goals:

- ⚖️ Feature and performance parity with synchronous and asynchronous processing
- 🚀 Zero-allocation processing
- ✂️ Minimal copying of data, instead exposing slices as spans

Data is read directly from the source, whether it's a @"System.String" or an array/@"System.ReadOnlyMemory`1".
When reading from a streaming source such as a @"System.IO.Stream" or @"System.IO.TextReader", the data is read into a
buffer and processed in chunks. After the current chunk has been processed, the tail of the data is copied to the start
and the next chunk is read. This process is repeated until the end of the stream is reached, when a scalar fallback
path handles the final tail. All other records are parsed with SIMD for maximum performance.

Unescaped fields must be done to a separate buffer to not mess with the original buffer. This is done either to
a stack-allocated buffer (up to 256 bytes/128 chars), or to a rented buffer from the configured memory pool.
Unescaping and buffer rewinding when reading are the only times when the library copies data when reading.

### Read-ahead

The read-ahead process works by reading as many CSV fields as possible from the currently available data.
Each field's data is stored in a [metadata-struct](https://github.com/ovska/FlameCsv/blob/main/FlameCsv.Core/Reading/Internal/Meta.cs),
which contains:

- The end index of the field in the data
- Whether the field is the last field in a record
- The count of special characters in the field
- Whether the special count refers to unix-style escapes instead of quotes
- The offset to the next field:
  - 1 for fields followed by a delimiter
  - 1 or 2 for fields followed by a newline
  - 0 for fields at the end of data (no trailing newline)

The data is sliced by using the previous field's end index and offset, and the current field's end index.

The metadata struct fits into 8 bytes (size of a `long`) by storing bit flags in some extra space (such
as the sign-bit of the end index), and the expectation that no single CSV field needs over 29 bits
(536&nbsp;870&nbsp;912) to store the quote/escape count.

Numerous optimization techniques have been trialed to improve performance until eventually settling on this approach.
Attempted optimizations include: double laning SIMD, storing EOL's in a separate array for fast TZCNT-lookups,
pre-calculating field/record ranges, using POPCNT to unroll control character parsing, and general shuffling and
reorganizing the code to produce better JIT ASM.

## Writing

The writing system is based on @"System.Buffers.IBufferWriter`1", allowing direct writes into the writer's buffer.
FlameCsv extends this functionality through @"FlameCsv.IO.ICsvBufferWriter`1" with additional features:
 - The writer keeps track of the written data, and contains the @"FlameCsv.IO.ICsvBufferWriter`1.NeedsFlush" property that
   determines if the internal buffers are close to full, and the written data should be flushed. The limit of "close to full"
   is arbitrarily determined, and future performance profiling might be needed (you are free to do so, or file an issue for it).
 - @"FlameCsv.IO.ICsvBufferWriter`1.Flush" and @"FlameCsv.IO.ICsvBufferWriter`1.FlushAsync(System.Threading.CancellationToken)"
   flush the writer to the destination, be it a @"System.IO.Stream", @"System.IO.TextWriter" or @"System.IO.Pipelines.PipeWriter".
 - @"FlameCsv.IO.ICsvBufferWriter`1.Complete(System.Exception)" and @"FlameCsv.IO.ICsvBufferWriter`1.CompleteAsync(System.Exception,System.Threading.CancellationToken)"
   that mirrors the pipelines-API. Completion disposes the writer and returns pooled buffers,
   and flushes the leftover data unless an exception was observed while writing.

When writing to @"System.IO.Stream" or @"System.IO.TextWriter", the destination memory is rented, and is written in large
blocks when flushing. Writing to @"System.IO.Pipelines.PipeWriter" delegates all of this to the pipe.

### Escaping and quoting

When writing, quoting and escaping is "optimistic", i.e., it's assumed most fields do not need to be quoted/escaped.
After writing each field, the written buffer is checked for characters that require quoting and escaping.
The written value is copied on character "forward", escaped if needed, and wrapped in quotes.

In the unfortunate case that the output buffer is just large enough to fit the unescaped value (worse case is value length
plus 2 for the wrapping quotes), a temporary buffer is used to copy the value, before writing the escaped value
to a large enough buffer.

Still to-do is a separate writing routine for @"FlameCsv.CsvFieldQuoting.Always?displayProperty=nameWithType"
that leaves off extra space at the start of the output buffer since a quote will be always written there. It has not
been implemented yet, with the expectation that this configuration is relatively rare.

## MemoryPool vs. ArrayPool

While arrays offer slightly better raw performance and avoid allocating a @"System.Buffers.IMemoryOwner`1",
they don't offer the flexibility of a memory pool. All pooling memory allocations of `T` are done with
@"FlameCsv.CsvOptions`1.MemoryPool?displayProperty=nameWithType", which gives the user a lot of control over
which memory to use. The pooled memory instances used for unescaping are reused
across the whole read-operation lifetime, so the allocations are minimal in the grand scheme of things.

This benefit extends to tests as well. As the reading code makes extensive use of @"System.Runtime.CompilerServices.Unsafe"
to avoid bounds checks when accessing the data numerous times, the validity of the unsafe code is tested by using
custom memory pools that allocate native memory with read-protected blocks before or after to ensure no out-of-bounds reads are done.
Such pools are also used in fuzz testing.

Note that buffers larger than @"System.Buffers.MemoryPool`1.MaxBufferSize?displayProperty=nameWithType" will be
rented from the shared array pool. This is unlikely unless your memory pool only supports tiny buffers, your fields are thousands of characters long,
or you intentionally use a small @"FlameCsv.IO.CsvIOOptions.BufferSize?displayProperty=nameWithType" value.


## Dynamic code generation

To optimize performance and maintain simplicity, runtime code generation is minimized.
Where possible, code is generated at _compile-time_ using T4 templates.
For example, the types read and write objects are generated this way.
This allows the JIT compiler to optimize much of the code as if it were handwritten.
Thanks to the JIT optimizations possible, if the reflection-based types are in cache, they are actually more performant
than the source generated version.

When runtime code generation is necessary (such as for creating getters for writing or object creation functions for reading),
the library uses the excellent [FastExpressionCompiler](https://github.com/dadhi/FastExpressionCompiler) library to
minimize compilation cost and improve delegate performance.

When using the @"source-generator", the library operates without any dynamic code or reflection.
