---
uid: architecture
---

# Architecture

## Foreword

This document explains the inner workings and design philosophy of FlameCsv.
While not required for using the library, it provides insights into the internals for those interested.

## Reading

FlameCSV's reading implementation is built on two goals:

- ⚖️ Feature parity and code sharing between synchronous and asynchronous operations
- 🚀 Zero-copy and zero-allocation processing

Data is read directly from the source, whether it's a @"System.String" or an array/@"System.ReadOnlyMemory`1".
When reading from a pipe, the same sequence is used as where the data was originally read from.
Streams and pipes can fragment the data across multiple buffers when reading, so the "first class async"-philosophy
made @"System.Buffers.ReadOnlySequence`1" a natural choice for the base data block for reading.
As much as possible is read from @"System.Buffers.ReadOnlySequence`1.First" (or in the case of an array or a string, all of it)
before falling back to working with the sequence directly.

The segmented nature of the data also requires deviation from the "zero-copy/allocation"-goal in the case of a single
CSV record being split across two or more sequences. A fragmented record is parsed using @"System.Buffers.SequenceReader`1"
and copied to a pooled buffer. This is one of the few cases in the reading routine when an allocation and a copy has to be
made. This is relatively rare in the grand scheme of things, as a buffer size of 4096 (a common default) can fit
quite many records before a buffer boundary. Likewise, reading from sequential data like an array avoids this altogether.

Another case when copying and a @"System.Buffers.IMemoryOwner`1" allocation must be done is when data needs to be unescaped.
A CSV field like `"John ""The Man"" Smith"` requires copying to get rid of the extra characters in the middle of it.
While in theory the data could be copied in-place by unsafely converting a @"System.ReadOnlySpan`1" to @"System.Span`1"
for example, the library chooses to respect the read-only contract of the types, and copies the unescaped field
into a pooled buffer. Quoted fields that do not require copying are simply sliced, e.g., `"Bond, James"` only requires
trimming the opening and closing quote. A stack-allocated buffer is used for fields under 256 bytes, so it's likely
you'll never see an allocation caused by unescaping. 

### Read-ahead

Warning: nerdy stuff ahead 🤓

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


## Writing

The writing system is based on @"System.Buffers.IBufferWriter`1", allowing direct writes into the writer's buffer.
FlameCsv extends this functionality through @"FlameCsv.IO.ICsvPipeWriter`1" with additional features:
 - The writer keeps tracks of the written data, and contains the @"FlameCsv.IO.ICsvPipeWriter`1.NeedsFlush" property that
   determines if the internal buffers are close to full, and the written data should be flushed. The limit of "close to full"
   is arbitrarily determined, and future performance profiling might be needed (you are free to do so, or file an issue for it).
 - @"FlameCsv.IO.ICsvPipeWriter`1.Flush" and @"FlameCsv.IO.ICsvPipeWriter`1.FlushAsync(System.Threading.CancellationToken)"
   flush the writer to the destination, be it a @"System.IO.Stream", @"System.IO.TextWriter" or @"System.IO.Pipelines.PipeWriter".
 - @"FlameCsv.IO.ICsvPipeWriter`1.Complete(System.Exception)" and @"FlameCsv.IO.ICsvPipeWriter`1.CompleteAsync(System.Exception,System.Threading.CancellationToken)"
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

Still to-do is a separate writing routine for @"FlameCsv.Writing.CsvFieldQuoting.Always?displayProperty=nameWithType"
that leaves off extra space at the start of the output buffer since a quote will be always written there. It has not
been implemented yet, with the expectation that this configuration is relatively rare.

## MemoryPool vs. ArrayPool

While arrays offer slightly better raw performance and avoid allocating a @"System.Buffers.IMemoryOwner`1",
they don't offer the flexibility of a memory pool. All pooling memory allocations of `T` are done with
@"FlameCsv.CsvOptions`1.MemoryPool?displayProperty=nameWithType", which gives the user a lot of control over
which memory to use. The pooled memory instances used for multisegment records and unescaping are also reused
across the whole read-operation lifetime, so the allocations are minimal in the grand scheme of things.

This benefit extends to tests as well. As the reading code makes extensive use of @"System.Runtime.CompilerServices.Unsafe"
to avoid bounds checks when accessing the data numerous times, the validity of the unsafe code is tested by using
a [custom memory pool](https://github.com/ovska/FlameCsv/blob/main/FlameCsv.Tests/Utilities/GuardedMemoryManager.cs)
that allocates native memory with read-protected blocks before and after to ensure no out-of-bounds reads are done.

Note that buffers larger than @"System.Buffers.MemoryPool`1.MaxBufferSize?displayProperty=nameWithType" will be
heap allocated. This is a non-issue for the default array-backed pool, and even for e.g., pagesize-limited
native memory pools would only apply if the CSV contained records or fields over 4096 bytes long.
You can track when this happens by collecting metrics from counter `memory.buffer_too_large` in the meter `FlameCsv`.
The default pool is array-backed, so you never need to worry about this when not using a custom implementation.
This behavior is possibly still subject to change, perhaps to throw an exception in this unlikely case to avoid
hidden allocations.


## Dynamic code generation

To optimize performance and maintain simplicity, runtime code generation is minimized.
Where possible, code is generated at _compile-time_ using T4 templates.
For example, the types that [read](https://github.com/ovska/FlameCsv/blob/main/FlameCsv.Core/Runtime/Materializer.Generated.cs)
and [write](https://github.com/ovska/FlameCsv/blob/main/FlameCsv.Core/Runtime/Dematerializer.Generated.cs) objects are generated this way.
This allows the JIT compiler to optimize much of the code as if it were handwritten.

When runtime code generation is necessary (such as for creating getters for writing or object creation functions for reading),
the library uses the excellent [FastExpressionCompiler](https://github.com/dadhi/FastExpressionCompiler) library to
minimize compilation cost and improve delegate performance.

When using the @"source-generator", the library operates without any dynamic code or reflection.
