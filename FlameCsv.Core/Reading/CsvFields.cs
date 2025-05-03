using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Extensions;
using FlameCsv.Reading.Internal;
using JetBrains.Annotations;

namespace FlameCsv.Reading;

/// <summary>
/// Contains the fields of a single CSV record.
/// </summary>
/// <typeparam name="T"></typeparam>
[PublicAPI]
[DebuggerDisplay("{ToString(),nq}")]
[DebuggerTypeProxy(typeof(CsvFields<>.CsvLineDebugView))]
public readonly struct CsvFields<T> : ICsvFields<T>
    where T : unmanaged, IBinaryInteger<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvFields(CsvReader<T> reader, ReadOnlyMemory<T> data, ArraySegment<Meta> fieldMeta)
    {
        Reader = reader;
        Data = data;
        _fieldMeta = fieldMeta;
    }

    private readonly ArraySegment<Meta> _fieldMeta;

    /// <summary>
    /// Raw value the fields point to.
    /// </summary>
    internal ReadOnlyMemory<T> Data { get; }

    /// <summary>
    /// Field end indexes and special character counts.
    /// </summary>
    /// <remarks>
    /// Contains one extra field at start denoting the start index inf <see cref="Data"/>.
    /// </remarks>
    internal ReadOnlySpan<Meta> Fields
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _fieldMeta.AsSpanUnsafe();
    }

    internal CsvReader<T> Reader { get; }

    /// <summary>
    /// Returns length of the raw record.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetRecordLength(bool includeTrailingNewline = false)
    {
        ReadOnlySpan<Meta> fields = Fields;
        int start = fields[0].NextStart;
        int end = includeTrailingNewline ? fields[^1].NextStart : fields[^1].End;
        return end - start;
    }

    /// <summary>
    /// Data of the raw record, not including possible trailing newline.
    /// </summary>
    public ReadOnlyMemory<T> Record
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ReadOnlySpan<Meta> fields = Fields;
            return Data[fields[0].NextStart..fields[^1].End];
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (Fields.IsEmpty)
        {
            return $"{{ CsvFields<{Token<T>.Name}>: Empty }}";
        }

        return $"{{ CsvFields<{Token<T>.Name}>[{Fields.Length - 1}]: \"{Reader.Options.GetAsString(Record.Span)}\" }}";
    }

    private class CsvLineDebugView
    {
        public CsvLineDebugView(CsvFields<T> fields)
        {
            var reader = new CsvFieldsRef<T>(in fields);

            Items = new string[reader.FieldCount];

            for (int i = 0; i < reader.FieldCount; i++)
            {
                Items[i] = CsvOptions<T>.Default.GetAsString(reader[i]);
            }
        }

        // ReSharper disable once CollectionNeverQueried.Local
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public string[] Items { get; }
    }

    /// <summary>
    /// Number of fields in the record.
    /// </summary>
    public int FieldCount => Fields.Length - 1;

    ReadOnlySpan<T> ICsvFields<T>.this[int index] => GetField(index);

    /// <summary>
    /// Returns the value of a field.
    /// </summary>
    /// <param name="index">Zero-based field index</param>
    /// <param name="raw">Whether to return the field unescaped</param>
    /// <returns>The field value</returns>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="index"/> is out of range</exception>
    /// <seealso cref="FieldCount"/>
    public ReadOnlySpan<T> GetField(int index, bool raw = false)
    {
        ReadOnlySpan<Meta> fields = Fields;

        if ((uint)index >= (uint)(fields.Length - 1))
            Throw.Argument_FieldIndex(index, fields.Length - 1);

        int start = fields[index].NextStart;
        ReadOnlySpan<T> data = Data.Span;

        if (raw)
        {
            return data[start..fields[index + 1].End];
        }

        return fields[index + 1].GetField(start: start, data: ref MemoryMarshal.GetReference(data), reader: Reader);
    }

    /// <summary>
    /// Returns an enumerator that iterates over the fields in the record.
    /// </summary>
    public Enumerator GetEnumerator()
    {
        Throw.IfDefaultStruct(Reader is null, typeof(CsvFields<T>));
        var reader = new CsvFieldsRef<T>(in this);
        return new Enumerator(reader);
    }

    /// <summary>
    /// Enumerates the fields in the record, unescaping them if needed.
    /// </summary>
    [PublicAPI]
    public ref struct Enumerator
    {
        private readonly CsvFieldsRef<T> _reader;
        private int _index;

        internal Enumerator(CsvFieldsRef<T> reader)
        {
            _reader = reader;
        }

        /// <summary>
        /// Current field in the enumerator.
        /// </summary>
        public ReadOnlySpan<T> Current { get; private set; }

        /// <summary>
        /// Attempts to read the next field in the record.
        /// </summary>
        /// <returns></returns>
        public bool MoveNext()
        {
            if ((uint)_index < (uint)_reader.FieldCount)
            {
                Current = _reader[_index++];
                return true;
            }

            Current = default;
            return false;
        }
    }

    /// <summary>
    /// Returns true if the record cannot be considered "self-contained", e.g., data needs to be copied to an
    /// unescape buffer.
    /// </summary>
    internal bool NeedsUnescapeBuffer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            foreach (var meta in Fields)
            {
                if (meta.SpecialCount > 2)
                    return true;
            }

            return false;
        }
    }
}
