using System.Buffers;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using FlameCsv.Reading.Internal;
using JetBrains.Annotations;

namespace FlameCsv.Reading;

/// <summary>
/// Represents a CSV record spanning one line, before the individual fields are read.
/// </summary>
/// <typeparam name="T"></typeparam>
[DebuggerDisplay("{ToString(),nq}")]
[EditorBrowsable(EditorBrowsableState.Never)]
[DebuggerTypeProxy(typeof(CsvLine<>.CsvLineDebugView))]
internal readonly ref struct CsvLine<T>(
    CsvParser<T> parser,
    ReadOnlyMemory<T> data,
    ReadOnlySpan<Meta> fields)
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Raw value the fields point to.
    /// </summary>
    public ReadOnlyMemory<T> Data { get; } = data;

    /// <summary>
    /// Field end indexes and special character counts.
    /// </summary>
    /// <remarks>
    /// Contains one extra field at start denoting the start index inf <see cref="Data"/>.
    /// </remarks>
    public ReadOnlySpan<Meta> Fields { get; } = fields;

    public CsvParser<T> Parser { get; } = parser;

    /// <summary>
    /// Length of the raw record, not including possible trailing newline.
    /// </summary>
    public int RecordLength
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Fields[^1].End - Fields[0].GetNextStart(Parser._newline.Length);
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

    public static LineEnumerator Enumerate(in ReadOnlySequence<T> data, CsvOptions<T>? options = null)
    {
        return new LineEnumerator(options ?? CsvOptions<T>.Default, data);
    }

    public ref struct LineEnumerator : IEnumerator<CsvLine<T>>
    {
        [HandlesResourceDisposal] private readonly CsvParser<T> _parser;
        private CsvLine<T> _current;

        public LineEnumerator(CsvOptions<T> options, in ReadOnlySequence<T> data)
        {
            _parser = CsvParser.Create(options);
            _parser.Reset(in data);
        }

        public bool MoveNext() => _parser.TryReadLine(out _current, false);

        public CsvLine<T> Current => _current;

        public void Reset() => throw new NotSupportedException();
        object IEnumerator.Current => throw new NotSupportedException();

        public void Dispose()
        {
            using (_parser) this = default;
        }

        public LineEnumerator GetEnumerator() => this;
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
                Items[i] = reader[i].ToString();
            }
        }

        // ReSharper disable once CollectionNeverQueried.Local
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public string[] Items { get; }
    }
}
