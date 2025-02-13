---
uid: architecture
---

# Architecture

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

> [!NOTE]
> **Why @"System.Buffers.MemoryPool`1" instead of @"System.Buffers.ArrayPool`1"?**<br/>
> While arrays offer slightly better raw performance and avoid allocating a @"System.Buffers.IMemoryOwner`1",
> they don't offer the flexibility of a memory pool. All pooling memory allocations of `T` are done with
> @"FlameCsv.CsvOptions`1.MemoryPool?displayProperty=nameWithType", which gives the user a lot of control over
> which memory to use. The pooled memory instances used for multisegment records and unescaping are also reused
> across the whole read-operation lifetime, so the allocations are minimal in the grand scheme of things.<br/>
> This benefit extends to tests as well. As the reading code makes extensive use of @"System.Runtime.CompilerServices.Unsafe"
> to avoid bounds checks when accessing the data numerous times, the validity of the unsafe code is tested by using
> a custom memory pool that allocates native memory with read-protected blocks before and after to ensure
> no out-of-bounds reads are done.


## Writing

The writing routines are based on @"System.Buffers.IBufferWriter`1" which allows consumers to write directly into the
writer's buffer. In practice, the writer returns an arbitrarily sized destination buffer, which is passed to
a converter or other code that writes directly to the buffer.

FlameCsv augments the writer type with a few extra APIs using @"FlameCsv.Writing.ICsvBufferWriter`1":
 - The writer keeps tracks of the written data, and contains the @"FlameCsv.Writing.ICsvBufferWriter`1.NeedsFlush" property that
   determines if the internal buffers are close to full, and the written data should be flushed. The limit of "close to full"
   is arbitrarily determined, and future performance profiling might be needed (you are free to do so, or file an issue for it).
 - @"FlameCsv.Writing.ICsvBufferWriter`1.Flush" and @"FlameCsv.Writing.ICsvBufferWriter`1.FlushAsync(System.Threading.CancellationToken)"
   flush the writer to the destination, be it a @"System.IO.Stream", "System.IO.TextWriter" or @"System.IO.Pipelines.PipeWriter".
 - @"FlameCsv.Writing.ICsvBufferWriter`1.Complete(System.Exception)" and @"FlameCsv.Writing.ICsvBufferWriter`1.CompleteAsync(System.Exception,System.Threading.CancellationToken)"
   that mirrors the pipelines-API. Completion disposes the writer and returns pooled buffers,
   and flushes the leftover data unless an exception was observed while writing.

When writing to @"System.IO.Stream" or @"System.IO.TextWriter", the destination memory is rented, and is written in large
blocks when flushing. Writing to @"System.IO.Pipelines.PipeWriter" delegates this aspect to the pipe and uses its' buffer directly.


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
