---
uid: architecture
---

# Architecture

## Foreword

This document is a general info-dump on the inner workings and design philosophy of FlameCsv. Not one wordo of it is
required (or perhaps even useful) to use the library. It should provide some insight for those interested in the
design choices and inner workings.

## Reading

FlameCSV's reading is built on two goals:

 - ⚖️ Feature parity and code sharing between sync and async
 - 🚀 Zero-copy and zero-allocation

Data is read directly from the source, whether it is a @"System.String" or an array/@"System.ReadOnlyMemory`1".
Similarly, when reading from a pipe, the same sequence is read from as where the data was originally read from.
Streams and pipes can fragment the data across multiple buffers when reading, so the "first class async"-philosophy
made @"System.Buffers.ReadOnlySequence`1" a natural choice for the base data block for reading.
As much as possible is read from @"System.Buffers.ReadOnlySequence`1.First" (or in the case of an array or a string, all of it)
before falling back to working with the sequence directly.

The segmented nature however also necessitates some deviation from the "zero-copy/allocation"-goal in the case of a single
CSV record being split across two or more sequences. A fragmented record is parsed using @"System.Buffers.SequenceReader`1"
and copied to a pooled buffer. This is one of the few cases in the reading routine when an allocation and a copy has to be
made. This is relatively rare in the grand scheme of things, as a buffer size of 4096 (a common default) can fit
quite many records before a buffer boundary. Likewise, reading from sequential data like an array avoids this altogether.

Another case when copying and a @"System.Buffers.IMemoryOwner`1" allocation must be done is when data needs to be unescaped.
A CSV field like `"John ""The Man"" Smith"` requires copying to get rid of the extra characters in the middle of it.
While in theory the data could be copied in-place by unsafely converting a @"System.ReadOnlySpan`1" to @"System.Span`1"
for example, the library chooses to respect the read-only contract of the types, and copies the unescaped field
into a pooled buffer. Quoted fields that do not require copying are simply sliced, e.g., `"Bond, James"` only requires
trimming the opening and closing quote.

### Read-ahead

Warning: nerdy stuff ahead 🤓

Read-ahead works by reading as much CSV fields as possible from the currently available data. The field data is stored
in a [Meta-struct](https://github.com/ovska/FlameCsv/blob/main/FlameCsv.Core/Reading/Internal/Meta.cs), that contains
the `End` index of the field in the data, whether the field is the last field in a record (`IsEOL`), and the amount of
special characters in the field.

Because the metadata only contains the end index, and additional special case exists that specifies the start of the data.
`End` and `IsEOL` are used to determine the start index of the next field, while the special count is used to determine
if the quotes need to be trimmed or the value needs to be unescaped. The very first field in the data is preceded by the special
"start"-value, which has and `End` of zero.

Since a field metadata only knows its' end index, a record with 5 fields actually contains 6 metadata fields, with the first
field being either the special start-value, or the `IsEOL` field of the previous record. The `IsEOL`-flag is used to
determine the total length of the field; in the case of 2-character newline, the next record starts 1 character later
than with a non-newline (only a delimiter) or single character newline (e.g., just `\n`).

The metadata struct has been jammed into 8 bytes (size of a `long`) by using ugly bit mask hacks and the expectation
that no single CSV field needs over 30 bits (1&nbsp;073&nbsp;741&nbsp;824) to store the quote/escape count.


## Writing

The writing routines are based on @"System.Buffers.IBufferWriter`1" which allows consumers to write directly into the
writer's buffer. In practice, the writer returns an arbitrarily sized destination buffer, which is passed to
a converter or other code that writes directly to the buffer.

FlameCsv augments the writer type with a few extra APIs using @"FlameCsv.Writing.ICsvBufferWriter`1":
 - The writer keeps tracks of the written data, and contains the @"FlameCsv.Writing.ICsvBufferWriter`1.NeedsFlush" property that
   determines if the internal buffers are close to full, and the written data should be flushed. The limit of "close to full"
   is arbitrarily determined, and future performance profiling might be needed (you are free to do so, or file an issue for it).
 - @"FlameCsv.Writing.ICsvBufferWriter`1.Flush" and @"FlameCsv.Writing.ICsvBufferWriter`1.FlushAsync(System.Threading.CancellationToken)"
   flush the writer to the destination, be it a @"System.IO.Stream", @"System.IO.TextWriter" or @"System.IO.Pipelines.PipeWriter".
 - @"FlameCsv.Writing.ICsvBufferWriter`1.Complete(System.Exception)" and @"FlameCsv.Writing.ICsvBufferWriter`1.CompleteAsync(System.Exception,System.Threading.CancellationToken)"
   that mirrors the pipelines-API. Completion disposes the writer and returns pooled buffers,
   and flushes the leftover data unless an exception was observed while writing.

When writing to @"System.IO.Stream" or @"System.IO.TextWriter", the destination memory is rented, and is written in large
blocks when flushing. Writing to @"System.IO.Pipelines.PipeWriter" delegates this aspect to the pipe and uses its' buffer directly.

### Escaping and quoting

When writing, quoting and escaping is "optimistic", i.e., it's assumed most fields do not need to be quoted/escaped.
After writing each field, the written buffer is checked for characters that require quoting and escaping.
The written value is copied on character "forward", escaped if needed, and wrapped in quotes.

In the unfortunate case that the output buffer is just large enough to fit the unescaped value, but not large enough
to hold the escape characters as well, as much as possible of the original value is unescaped in the writer's original
destination buffer, while the rest is unescaped to an extra stack-allocated span (or a rented buffer in some weird huge field
edge cases). The original destionation's worth is then advanced to the buffer reader, and the extra bytes advanced right after.

The justification for this slightly odd behavior is the fact that there is no way to know if a field needs to be escaped
or not beforehand, and for performance the converters should always format their value directly to the correct place in
the buffer so no extra copying needs to be done in the common case.

Still to-do is a separate writing routine for @"FlameCsv.Writing.CsvFieldQuoting.Always?displayProperty=nameWithType"
that leaves off extra space at the start of the output buffer since a quote will be always written there. As it stands,
even with this option an extra copy needs to be done. Also possible is an attribute to override field-quoting
on per-member basis, e.g. to always quote a certain property.

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


## Dynamic code generation

For performance and simplicity, runtime code generation is kept to a minimum. Whenever possible, the required code is generated
at _compile-time_ using T4 templates. Examples of this are the types that
[read](https://github.com/ovska/FlameCsv/blob/main/FlameCsv.Core/Runtime/Materializer.Generated.cs) and
[write](https://github.com/ovska/FlameCsv/blob/main/FlameCsv.Core/Runtime/Dematerializer.Generated.cs)
.NET types. This allows the JIT compiler to "see" the code it is running, and optimize it like hand-written code.

When code generation is necessary, such as creating the getters needed for writing, and the object creation function when reading,
[FastExpressionCompiler](https://github.com/dadhi/FastExpressionCompiler) is used to minimize the cost of compiling the expression
and to improve the compiled delegate's performance.

When using @"source-generator", no dynamic code or reflection is used at all.
