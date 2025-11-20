// using System.Buffers;
// using FlameCsv.Reading;
// using FlameCsv.Reading.Internal;

// namespace FlameCsv.IO.Internal;

// internal readonly struct Chunk<T> : IDisposable
//     where T : unmanaged, IBinaryInteger<T>
// {
//     public ReadOnlyMemory<T> Data { get; }

//     private readonly RecordBuffer _recordBuffer;
//     private readonly IDisposable _owner;

//     public Chunk(RecordBuffer recordBuffer, IMemoryOwner<T> owner)
//     {
//         Data = owner.Memory;
//         _owner = owner;
//     }

//     public void Dispose()
//     {
//         _recordBuffer.Dispose();
//         _owner.Dispose();
//     }

//     public bool TryPop(out CsvRecordRef<T> record)
//     {
//         if (_recordBuffer.TryPop(out RecordView view))
//         {
//             record = new(); // TODO
//             return true;
//         }

//         record = default;
//         return false;
//     }
// }

// internal abstract class ParallelReader<T>
//     where T : unmanaged, IBinaryInteger<T>
// {
//     private readonly CsvOptions<T> _options;
//     private readonly int _bufferSize;

//     private readonly CsvTokenizer<T> _tokenizer;
//     private readonly CsvScalarTokenizer<T> _scalarTokenizer;

//     public bool IsCompleted { get; private set; }

//     protected ParallelReader(CsvOptions<T> options, CsvIOOptions ioOptions)
//     {
//         _options = options;
//         _bufferSize = ioOptions.BufferSize;
//     }

//     protected abstract int ReadCore(Span<T> buffer);
//     protected abstract ValueTask<int> ReadAsyncCore(Memory<T> buffer, CancellationToken cancellationToken);

//     private bool TryAdvance(IMemoryOwner<T> owner, int read, out Chunk<T> chunk)
//     {
//         if (read == 0)
//         {
//             // TODO: handle tail
//             chunk = default;
//             return false;
//         }

//         RecordBuffer recordBuffer = new(bufferSize: 192); // TODO tweak

//         FieldBuffer destination = recordBuffer.GetUnreadBuffer(_tokenizer.MinimumFieldBufferSize, out int startIndex);
//         int count = _tokenizer.Tokenize(destination, startIndex, owner.Memory.Span.Slice(0, read));
//         recordBuffer.SetFieldsRead(count);
//     }
// }
