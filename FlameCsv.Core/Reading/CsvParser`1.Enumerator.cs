using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reading.Internal;
using FlameCsv.Utilities;
using JetBrains.Annotations;

namespace FlameCsv.Reading;

partial class CsvParser<T>
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
        private readonly CsvParser<T> _parser;

        internal RecordEnumerable(CsvParser<T> parser)
        {
            _parser = parser;
        }

        /// <inheritdoc cref="CsvParser{T}.ParseRecords"/>
        public Enumerator GetEnumerator()
        {
            Throw.IfDefaultStruct(_parser is null, typeof(RecordEnumerable));
            return new(_parser);
        }
    }


    /// <summary>
    /// Enumerates records from the parser.
    /// </summary>
    public readonly struct RecordAsyncEnumerable : IAsyncEnumerable<CsvFieldsRef<T>>
    {
        private readonly CsvParser<T> _parser;
        private readonly CancellationToken _cancellationToken;

        internal RecordAsyncEnumerable(CsvParser<T> parser, CancellationToken cancellationToken)
        {
            _parser = parser;
            _cancellationToken = cancellationToken;
        }

        /// <inheritdoc cref="CsvParser{T}.ParseRecordsAsync"/>
        public AsyncEnumerator GetAsyncEnumerator()
        {
            Throw.IfDefaultStruct(_parser is null, typeof(RecordAsyncEnumerable));
            return new(_parser, _cancellationToken);
        }

        IAsyncEnumerator<CsvFieldsRef<T>> IAsyncEnumerable<CsvFieldsRef<T>>.GetAsyncEnumerator(
            CancellationToken cancellationToken)
        {
            return new AsyncEnumerator(_parser, cancellationToken);
        }
    }

    /// <summary>
    /// Enumerator for raw CSV record fields.
    /// </summary>
    [PublicAPI]
    [SkipLocalsInit]
    public ref struct Enumerator : IDisposable
    {
        [HandlesResourceDisposal] private readonly CsvParser<T> _parser;
        private EnumeratorStack _stackMemory;

        private ref Meta _meta;
        private int _metaLength;

        private ref T _data;

        internal Enumerator(CsvParser<T> parser)
        {
            _parser = parser;
        }

        /// <summary>
        /// Current record.
        /// </summary>
        [UnscopedRef]
        public CsvFieldsRef<T> Current => new(_parser, ref _data, ref _meta, _metaLength, _stackMemory.AsSpan());

        /// <summary>
        /// Attempts to read the next record.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if a record was read; otherwise, <see langword="false"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            CsvParser<T> parser = _parser;

            if (parser._metaIndex < parser._metaCount)
            {
                ref Meta metaRef = ref MemoryMarshal.GetArrayDataReference(parser._metaArray);

                if (Meta.TryFindNextEOL(
                        first: ref Unsafe.Add(ref metaRef, 1 + parser._metaIndex),
                        end: parser._metaCount - parser._metaIndex + 1,
                        index: out int fieldCount))
                {
                    _meta = ref Unsafe.Add(ref metaRef, parser._metaIndex);
                    _metaLength = fieldCount + 1;
                    parser._metaIndex += fieldCount;
                    return true;
                }
            }

            return MoveNextSlow();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool MoveNextSlow()
        {
            if (_parser.TryReadUnbuffered(out CsvFields<T> fields, isFinalBlock: false))
            {
                goto ConstructValue;
            }

            while (_parser.TryAdvanceReader())
            {
                if (_parser.TryReadLine(out fields, isFinalBlock: false))
                {
                    goto ConstructValue;
                }
            }

            if (!_parser.TryReadUnbuffered(out fields, isFinalBlock: true))
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
            _parser.Dispose();
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
            public CsvFields<T> Value;

            // ReSharper disable once UnassignedField.Local
            public EnumeratorStack Stack;
        }

        private readonly CsvParser<T> _parser;
        private readonly CancellationToken _cancellationToken;
        private readonly Box _field;

        internal AsyncEnumerator(CsvParser<T> parser, CancellationToken cancellationToken)
        {
            _parser = parser;
            _cancellationToken = cancellationToken;
            _field = new();
        }

        /// <summary>
        /// Returns a read-only reference to the field value that <see cref="Current"/> is constructed from.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public ref readonly CsvFields<T> UnsafeGetFields() => ref _field.Value;

        /// <inheritdoc cref="Enumerator.Current"/>
        public CsvFieldsRef<T> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(in _field.Value, Unsafe.AsRef(in _field.Stack).AsSpan());
        }

        /// <inheritdoc cref="Enumerator.MoveNext"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<bool> MoveNextAsync()
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                return ValueTask.FromCanceled<bool>(_cancellationToken);
            }

            if (_parser.TryGetBuffered(out _field.Value))
            {
                return new ValueTask<bool>(true);
            }

            return MoveNextSlowAsync();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private async ValueTask<bool> MoveNextSlowAsync()
        {
            if (_parser.TryReadUnbuffered(out _field.Value, isFinalBlock: false))
            {
                return true;
            }

            while (await _parser.TryAdvanceReaderAsync(_cancellationToken).ConfigureAwait(false))
            {
                if (_parser.TryReadLine(out _field.Value, isFinalBlock: false))
                {
                    return true;
                }
            }

            return _parser.TryReadUnbuffered(out _field.Value, isFinalBlock: true);
        }

        /// <inheritdoc cref="Enumerator.Dispose"/>
        public ValueTask DisposeAsync()
        {
            _field.Value = default; // don't hold on to data
            return _parser.DisposeAsync();
        }
    }

    [SkipLocalsInit]
    [InlineArray(Length)]
    internal struct EnumeratorStack
    {
        public const int Length = 256;
        public byte elem0;
    }
}

[SkipLocalsInit]
file static class LocalExtensions
{
    private const int Length = CsvParser<byte>.EnumeratorStack.Length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> AsSpan<T>(ref this CsvParser<T>.EnumeratorStack memory)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (typeof(T) == typeof(byte))
            return MemoryMarshal.CreateSpan(ref Unsafe.As<byte, T>(ref memory.elem0), Length);

        if (typeof(T) == typeof(char))
            return MemoryMarshal.CreateSpan(ref Unsafe.As<byte, T>(ref memory.elem0), Length / sizeof(char));

        return MemoryMarshal.Cast<byte, T>(MemoryMarshal.CreateSpan(ref memory.elem0, Length));
    }
}
