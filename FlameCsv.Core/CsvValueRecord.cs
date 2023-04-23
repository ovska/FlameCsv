using System.Buffers;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Configuration;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv;

/// <inheritdoc cref="ICsvRecord{T}"/>
[DebuggerTypeProxy(typeof(CsvValueRecord<>.CsvRecordDebugView))]
public readonly partial struct CsvValueRecord<T> : ICsvRecord<T>, IEnumerable<ReadOnlyMemory<T>> where T : unmanaged, IEquatable<T>
{
    public long Position { get; }
    public int Line { get; }
    public ReadOnlyMemory<T> Data { get; }
    public CsvDialect<T> Dialect => _state.Dialect;

    public bool HasHeader => _state._header is not null;

    public ReadOnlyMemory<T> this[int index] => GetField(index);
    public ReadOnlyMemory<T> this[string name] => GetField(name);

    internal int TotalFieldLength => _state.TotalFieldLength;

    internal readonly CsvEnumerationState<T> _state;
    internal readonly CsvReaderOptions<T> _options;
    private readonly int _version;
    private readonly RecordMeta _meta;

    public CsvValueRecord(
        ReadOnlyMemory<T> data,
        CsvReaderOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.MakeReadOnly();

        Position = 0;
        Line = 1;
        Data = data;
        _options = options;
        _state = new CsvEnumerationState<T>(options);
        _meta = _state.Dialect.GetRecordMeta(data, options.AllowContentInExceptions);
        _version = _state.Initialize(data, _meta);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvValueRecord(
        long position,
        int line,
        ReadOnlyMemory<T> data,
        CsvReaderOptions<T> options,
        RecordMeta meta,
        CsvEnumerationState<T> state)
    {
        Position = position;
        Line = line;
        Data = data;
        _options = options;
        _state = state;
        _meta = meta;
        _version = _state.Initialize(data, meta);
    }

    /// <inheritdoc cref="ICsvRecord{T}.GetField(string)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyMemory<T> GetField(string name)
    {
        _state.EnsureVersion(_version);

        if (!_state.TryGetHeaderIndex(name, out int index))
        {
            ThrowHeaderException(name);
        }

        return GetField(index);
    }

    /// <inheritdoc cref="ICsvRecord{T}.GetField(int)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyMemory<T> GetField(int index)
    {
        _state.EnsureVersion(_version);
 
        if (!_state.TryGetAtIndex(index, out ReadOnlyMemory<T> field))
        {
            ThrowIndexException(index);
        }

        return field;
    }

    /// <inheritdoc cref="ICsvRecord{T}.GetFieldCount"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetFieldCount()
    {
        _state.EnsureVersion(_version);

        return _state.GetFieldCount();
    }

    public bool TryGetValue<TValue>(int index, [MaybeNullWhen(false)] out TValue value) => TryGetValue(index, out value, out _);

    /// <inheritdoc cref="ICsvRecord{T}.TryGetValue{TValue}(int, out TValue, out CsvGetValueReason)"/>
    public bool TryGetValue<TValue>(
        int index,
        [MaybeNullWhen(false)] out TValue value,
        out CsvGetValueReason reason)
    {
        _state.EnsureVersion(_version);
    
        if (!_state.TryGetAtIndex(index, out ReadOnlyMemory<T> field))
        {
            reason = CsvGetValueReason.FieldNotFound;
            value = default;
            return false;
        }

        if (_options.TryGetParser<TValue>() is not { } parser)
        {
            reason = CsvGetValueReason.NoParserFound;
            value = default;
            return false;
        }

        if (!parser.TryParse(field.Span, out value))
        {
            reason = CsvGetValueReason.UnparsableValue;
            value = default;
            return false;
        }

        reason = CsvGetValueReason.Success;
        return true;
    }

    public bool TryGetValue<TValue>(string name, [MaybeNullWhen(false)] out TValue value) => TryGetValue(name, out value, out _);

    public bool TryGetValue<TValue>(string name, [MaybeNullWhen(false)] out TValue value, out CsvGetValueReason reason)
    {
        _state.EnsureVersion(_version);
    
        if (!_state.TryGetHeaderIndex(name, out int index))
        {
            value = default;
            reason = CsvGetValueReason.HeaderNotFound;
            return false;
        }

        return TryGetValue(index, out value, out reason);
    }

    public TValue GetField<TValue>(string name)
    {
        _state.EnsureVersion(_version);
     
        if (!_state.TryGetHeaderIndex(name, out int index))
        {
            ThrowHeaderException(name);
        }

        return GetField<TValue>(index);
    }

    /// <inheritdoc cref="ICsvRecord{T}.GetField{TValue}(int)"/>
    public TValue GetField<TValue>(int index)
    {
        _state.EnsureVersion(_version);
      
        if (!_state.TryGetAtIndex(index, out ReadOnlyMemory<T> field))
        {
            ThrowIndexException(index);
        }

        var parser = _options.GetParser<TValue>();

        if (!parser.TryParse(field.Span, out var value))
        {
            ThrowParseException(field.Span, typeof(TValue), parser);
        }

        return value;
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowHeaderException(string name)
    {
        Debug.Assert(_state._header is not null);

        string msg = _options.AllowContentInExceptions
            ? $"Header \"{name}\" was not found among the CSV headers: {string.Join(", ", _state._header.Keys)}"
            : "Header not found among the CSV headers.";

        throw new ArgumentException(msg, nameof(name));
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowIndexException(int index)
    {
        string? knownColumn;

        try
        {
            knownColumn = $"(there were {_state.GetFieldCount()} columns in the record)";
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch
        {
            knownColumn = "<could not get field count>";
        }
#pragma warning restore CA1031 // Do not catch general exception types

        throw new ArgumentOutOfRangeException(
            nameof(index),
            $"Could not get column at index {index} {knownColumn}.");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowParseException(ReadOnlySpan<T> data, Type parsedType, object parser)
    {
        throw new CsvParseException(
            $"Failed to parse {parsedType.FullName} using {parser.GetType().FullName} " +
            $"from {data.AsPrintableString(_options.AllowContentInExceptions, _state.Dialect)}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CsvFieldEnumerator<T> GetEnumerator() => new(value: Data, state: _state, meta: _meta);

    IEnumerator<ReadOnlyMemory<T>> IEnumerable<ReadOnlyMemory<T>>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private sealed class CsvRecordDebugView
    {
        private readonly CsvValueRecord<T> _record;

        public CsvRecordDebugView(CsvValueRecord<T> record) => _record = record;

        public int Line => _record.Line;

        public long Position => _record.Position;

        public string[] Headers => _record._state._header?.Keys.ToArray() ?? Array.Empty<string>();

        public ReadOnlyMemory<T>[] Fields => _record.AsEnumerable().ToArray();

        public string[] FieldValues => _record._options is ICsvStringConfiguration<T> cfg
            ? Fields.Select(f => cfg.GetTokensAsString(f.Span)).ToArray()
            : Array.Empty<string>();
    }
}
