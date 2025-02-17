using System.Diagnostics;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using FlameCsv.Reading.Internal;
using JetBrains.Annotations;

namespace FlameCsv.Reading;

/// <summary>
/// Represents a CSV record spanning one line, before the individual fields are read.
/// </summary>
/// <typeparam name="T"></typeparam>
[PublicAPI]
[DebuggerDisplay("{ToString(),nq}")]
[DebuggerTypeProxy(typeof(CsvLine<>.CsvLineDebugView))]
public readonly struct CsvLine<T> : ICsvRecordFields<T> where T : unmanaged, IBinaryInteger<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvLine(CsvParser<T> parser, ReadOnlyMemory<T> data, ArraySegment<Meta> fields)
    {
        Parser = parser;
        Data = data;
        _fields = fields;
    }

    private readonly ArraySegment<Meta> _fields;

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
        get => _fields.AsSpan();
    }

    internal CsvParser<T> Parser { get; }

    /// <summary>
    /// Length of the raw record, not including possible trailing newline.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetRecordLength(bool includeTrailingNewline = false)
    {
        int start = Fields[0].GetNextStart(Parser._newline.Length);
        int end = includeTrailingNewline ? Fields[^1].GetNextStart(Parser._newline.Length) : Fields[^1].GetNextStart(0);
        return end - start;
    }

    /// <summary>
    /// Data of the raw record, not including possible trailing newline.
    /// </summary>
    public ReadOnlyMemory<T> Record
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Data[Fields[0].GetNextStart(Parser._newline.Length)..Fields[^1].End];
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (Fields.IsEmpty)
        {
            return $"{{ CsvLine<{Token<T>.Name}>: Empty }}";
        }

        return $"{{ CsvLine<{Token<T>.Name}>[{Fields.Length - 1}]: \"{Parser.Options.GetAsString(Record.Span)}\" }}";
    }

    private class CsvLineDebugView
    {
        public CsvLineDebugView(CsvLine<T> line)
        {
            Span<T> unescapeBuffer = stackalloc T[Token<T>.StackLength];
            var reader = new MetaFieldReader<T>(in line, unescapeBuffer);

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

    ReadOnlySpan<T> ICsvRecordFields<T>.this[int index] => GetField(index);

    /// <summary>
    /// Returns the value of a field.
    /// </summary>
    /// <param name="index">Zero-based field index</param>
    /// <param name="raw">Whether to return the field unescaped</param>
    /// <param name="buffer">Optional buffer to use when unescaping fields with embedded quotes/escapes</param>
    /// <returns>The field value</returns>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="index"/> is out of range</exception>
    /// <seealso cref="FieldCount"/>
    public ReadOnlySpan<T> GetField(int index, bool raw = false, Span<T> buffer = default)
    {
        if ((uint)index >= (uint)(Fields.Length - 1))
            Throw.Argument_FieldIndex(index, Fields.Length - 1);

        int start = Fields[index].GetNextStart(Parser._newline.Length);
        ReadOnlySpan<T> data = Data.Span;

        if (raw)
        {
            return data[start..Fields[index + 1].End];
        }

        return Fields[index + 1]
            .GetField(
                dialect: in Parser._dialect,
                start: start,
                data: data,
                buffer: buffer,
                getBuffer: Parser.GetUnescapeBuffer);
    }

    /// <summary>
    /// Returns an enumerator that iterates over the fields in the record.
    /// </summary>
    public Enumerator GetEnumerator()
    {
        Throw.IfDefaultStruct(Parser is null, typeof(CsvLine<T>));
        var reader = new MetaFieldReader<T>(in this, Parser.GetUnescapeBuffer);
        return new Enumerator(reader);
    }

    /// <summary>
    /// Enumerates the fields in the record, unescaping them if needed.
    /// </summary>
    [PublicAPI]
    public ref struct Enumerator
    {
        private readonly MetaFieldReader<T> _reader;
        private int _index;

        internal Enumerator(MetaFieldReader<T> reader)
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
                if (meta.SpecialCount > 2) return true;
            }

            return false;
        }
    }
}
