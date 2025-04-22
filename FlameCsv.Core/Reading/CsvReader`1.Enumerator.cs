using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Extensions;
using FlameCsv.Reading.Internal;
using JetBrains.Annotations;

namespace FlameCsv.Reading;

partial class CsvReader<T>
{
    /// <summary>
    /// Returns an enumerator that iterates through the CSV data.
    /// </summary>
    /// <remarks>
    /// The enumerator advances the inner reader and parser, and disposes them after use.
    /// </remarks>
    [HandlesResourceDisposal]
    public RecordEnumerable ParseRecords() => new(this);

    /// <summary>
    /// Returns an enumerator that asynchronously iterates through the CSV data.
    /// </summary>
    /// <remarks>
    /// The enumerator advances the inner reader and parser, and disposes them after use.
    /// </remarks>
    [HandlesResourceDisposal]
    public RecordAsyncEnumerable ParseRecordsAsync(CancellationToken cancellationToken = default)
        => new(this, cancellationToken);

    /// <summary>
    /// Enumerates records from the parser.
    /// </summary>
    public readonly struct RecordEnumerable
    {
        private readonly CsvReader<T> _reader;

        internal RecordEnumerable(CsvReader<T> reader)
        {
            _reader = reader;
        }

        /// <inheritdoc cref="CsvReader{T}.ParseRecords"/>
        public Enumerator GetEnumerator()
        {
            Throw.IfDefaultStruct(_reader is null, typeof(RecordEnumerable));
            return new(_reader);
        }
    }


    /// <summary>
    /// Enumerates records from the parser.
    /// </summary>
    public readonly struct RecordAsyncEnumerable : IAsyncEnumerable<CsvFieldsRef<T>>
    {
        private readonly CsvReader<T> _reader;
        private readonly CancellationToken _cancellationToken;

        internal RecordAsyncEnumerable(CsvReader<T> reader, CancellationToken cancellationToken)
        {
            _reader = reader;
            _cancellationToken = cancellationToken;
        }

        /// <inheritdoc cref="CsvReader{T}.ParseRecordsAsync"/>
        public AsyncEnumerator GetAsyncEnumerator()
        {
            Throw.IfDefaultStruct(_reader is null, typeof(RecordAsyncEnumerable));
            return new(_reader, _cancellationToken);
        }

        IAsyncEnumerator<CsvFieldsRef<T>> IAsyncEnumerable<CsvFieldsRef<T>>.GetAsyncEnumerator(
            CancellationToken cancellationToken)
        {
            return new AsyncEnumerator(_reader, cancellationToken);
        }
    }

    /// <summary>
    /// Enumerator for raw CSV record fields.
    /// </summary>
    [PublicAPI]
    [SkipLocalsInit]
    public ref struct Enumerator : IDisposable
    {
        [HandlesResourceDisposal] private readonly CsvReader<T> _reader;
        private EnumeratorStack _stackMemory;

        private ref Meta _meta;
        private int _metaLength;

        private ref T _data;

        internal Enumerator(CsvReader<T> reader)
        {
            _reader = reader;
        }

        /// <summary>
        /// Current record.
        /// </summary>
        [UnscopedRef]
        public CsvFieldsRef<T> Current => new(_reader, ref _data, ref _meta, _metaLength, _stackMemory.AsSpan<T>());

        /// <summary>
        /// Attempts to read the next record.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if a record was read; otherwise, <see langword="false"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            CsvReader<T> reader = _reader;

            if (reader._metaBuffer.TryPop(out ArraySegment<Meta> meta))
            {
                _meta = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(meta.Array!), meta.Offset);
                _metaLength = meta.Count;
                return true;
            }

            return MoveNextSlow();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool MoveNextSlow()
        {
            if (_reader.TryFillBuffer(out CsvFields<T> fields))
            {
                goto ConstructValue;
            }

            while (_reader.TryAdvanceReader())
            {
                if (_reader.TryReadLine(out fields))
                {
                    goto ConstructValue;
                }
            }

            if (!_reader.TryReadLine(out fields))
            {
                _data = ref Unsafe.NullRef<T>();
                _meta = ref Unsafe.NullRef<Meta>();
                _metaLength = 0;
                return false;
            }

        ConstructValue:
            _data = ref MemoryMarshal.GetReference(fields.Data.Span);
            _meta = ref MemoryMarshal.GetReference(fields.Fields);
            _metaLength = fields.Fields.Length;
            return true;
        }

        /// <summary>
        /// Disposes the parser.
        /// </summary>
        public void Dispose()
        {
            _reader.Dispose();
            _metaLength = 0;
            _meta = ref Unsafe.NullRef<Meta>();
            _data = ref Unsafe.NullRef<T>();
        }
    }

    /// <inheritdoc cref="Enumerator"/>
    [PublicAPI]
    public readonly struct AsyncEnumerator : IAsyncEnumerator<CsvFieldsRef<T>>
    {
        // the asyncenumerator struct needs to be readonly to play nice with async
        private sealed class Box
        {
            public CsvFields<T> Fields;

            // ReSharper disable once UnassignedField.Local
#pragma warning disable CS0649
            public EnumeratorStack Memory;
#pragma warning restore CS0649
        }

        private readonly CsvReader<T> _reader;
        private readonly CancellationToken _cancellationToken;
        private readonly Box _box;

        internal AsyncEnumerator(CsvReader<T> reader, CancellationToken cancellationToken)
        {
            _reader = reader;
            _cancellationToken = cancellationToken;
            _box = new();
        }

        /// <summary>
        /// Returns a read-only reference to the field value that <see cref="Current"/> is constructed from.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public ref readonly CsvFields<T> UnsafeGetFields() => ref _box.Fields;

        /// <inheritdoc cref="Enumerator.Current"/>
        public CsvFieldsRef<T> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(in _box.Fields, Unsafe.AsRef(in _box.Memory).AsSpan<T>());
        }

        /// <inheritdoc cref="Enumerator.MoveNext"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<bool> MoveNextAsync()
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                return ValueTask.FromCanceled<bool>(_cancellationToken);
            }

            if (_reader.TryGetBuffered(out _box.Fields))
            {
                return new ValueTask<bool>(true);
            }

            return MoveNextSlowAsync();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private async ValueTask<bool> MoveNextSlowAsync()
        {
            if (_reader.TryFillBuffer(out _box.Fields))
            {
                return true;
            }

            while (await _reader.TryAdvanceReaderAsync(_cancellationToken).ConfigureAwait(false))
            {
                if (_reader.TryReadLine(out _box.Fields))
                {
                    return true;
                }
            }

            return _reader.TryReadLine(out _box.Fields);
        }

        /// <inheritdoc cref="Enumerator.Dispose"/>
        public ValueTask DisposeAsync()
        {
            _box.Fields = default; // don't hold on to data
            return _reader.DisposeAsync();
        }
    }
}

[SkipLocalsInit]
[InlineArray(Length)]
internal struct EnumeratorStack
{
    public const int Length = 256;
    public byte elem0;
}

[SkipLocalsInit]
file static class LocalExtensions
{
    private const int Length = EnumeratorStack.Length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> AsSpan<T>(ref this EnumeratorStack memory)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (typeof(T) == typeof(byte))
            return MemoryMarshal.CreateSpan(ref Unsafe.As<byte, T>(ref memory.elem0), Length);

        if (typeof(T) == typeof(char))
            return MemoryMarshal.CreateSpan(ref Unsafe.As<byte, T>(ref memory.elem0), Length / sizeof(char));

        return MemoryMarshal.Cast<byte, T>(MemoryMarshal.CreateSpan(ref memory.elem0, Length));
    }
}
