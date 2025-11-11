using System.Collections;
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
    public RecordAsyncEnumerable ParseRecordsAsync(CancellationToken cancellationToken = default) =>
        new(this, cancellationToken);

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
            if (_reader is null)
            {
                Throw.InvalidOp_DefaultStruct(typeof(RecordEnumerable));
            }

            return new(_reader);
        }
    }

    /// <summary>
    /// Enumerates records from the parser.
    /// </summary>
    public readonly struct RecordAsyncEnumerable : IAsyncEnumerable<CsvRecordRef<T>>
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
            if (_reader is null)
            {
                Throw.InvalidOp_DefaultStruct(typeof(RecordAsyncEnumerable));
            }

            return new(_reader, _cancellationToken);
        }

        IAsyncEnumerator<CsvRecordRef<T>> IAsyncEnumerable<CsvRecordRef<T>>.GetAsyncEnumerator(
            CancellationToken cancellationToken
        )
        {
            return new AsyncEnumerator(_reader, cancellationToken);
        }
    }

    /// <summary>
    /// Enumerator for raw CSV record fields.
    /// </summary>
    [PublicAPI]
    [SkipLocalsInit]
    public ref struct Enumerator : IEnumerator<CsvRecordRef<T>>
    {
        [HandlesResourceDisposal]
        private readonly CsvReader<T> _reader;

        private RecordView _view;
        private ref T _data;

        internal Enumerator(CsvReader<T> reader)
        {
            _reader = reader;
        }

        /// <summary>
        /// Current record.
        /// </summary>
        [UnscopedRef]
        public CsvRecordRef<T> Current => new(_reader, ref _data, _view);

        /// <summary>
        /// Attempts to read the next record.
        /// </summary>
        /// <returns>
        /// <c>true</c> if a record was read; otherwise, <c>false</c>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_reader._recordBuffer.TryPop(out _view))
            {
                return true;
            }

            return MoveNextSlow();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool MoveNextSlow()
        {
            if (_reader.TryFillBuffer(out CsvSlice<T> slice))
            {
                goto ConstructValue;
            }

            while (_reader.TryAdvanceReader())
            {
                if (_reader.TryReadLine(out slice))
                {
                    goto ConstructValue;
                }
            }

            if (!_reader.TryReadLine(out slice))
            {
                _data = ref Unsafe.NullRef<T>();
                _view = default;
                return false;
            }

            ConstructValue:
            _data = ref MemoryMarshal.GetReference(slice.Data.Span);
            _view = slice.Record;
            return true;
        }

        /// <summary>
        /// Disposes the parser.
        /// </summary>
        public void Dispose()
        {
            _reader.Dispose();
            _view = default;
            _data = ref Unsafe.NullRef<T>();
        }

        CsvRecordRef<T> IEnumerator<CsvRecordRef<T>>.Current => new(_reader, ref _data, _view);
        readonly object IEnumerator.Current => throw new NotSupportedException();

        bool IEnumerator.MoveNext() => MoveNext();

        void IEnumerator.Reset()
        {
            _reader.Reset();
            _view = default;
            _data = ref Unsafe.NullRef<T>();
        }
    }

    /// <inheritdoc cref="Enumerator"/>
    [PublicAPI]
    [SkipLocalsInit]
    public sealed class AsyncEnumerator : IAsyncEnumerator<CsvRecordRef<T>>
    {
        private readonly CsvReader<T> _reader;
        private readonly CancellationToken _cancellationToken;
        private CsvSlice<T> _slice;

        internal AsyncEnumerator(CsvReader<T> reader, CancellationToken cancellationToken)
        {
            _reader = reader;
            _cancellationToken = cancellationToken;
        }

        /// <inheritdoc cref="Enumerator.Current"/>
        public CsvRecordRef<T> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(in _slice);
        }

        /// <inheritdoc cref="Enumerator.MoveNext"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<bool> MoveNextAsync()
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                return ValueTask.FromCanceled<bool>(_cancellationToken);
            }

            if (!_reader.TryGetBuffered(out _slice))
            {
                return MoveNextSlowAsync();
            }

            return new ValueTask<bool>(true);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private async ValueTask<bool> MoveNextSlowAsync()
        {
            if (_reader.TryFillBuffer(out _slice))
            {
                return true;
            }

            while (await _reader.TryAdvanceReaderAsync(_cancellationToken).ConfigureAwait(false))
            {
                if (_reader.TryReadLine(out _slice))
                {
                    return true;
                }
            }

            return _reader.TryReadLine(out _slice);
        }

        /// <inheritdoc cref="Enumerator.Dispose"/>
        public ValueTask DisposeAsync()
        {
            _slice = default; // don't hold on to data
            return _reader.DisposeAsync();
        }
    }
}
