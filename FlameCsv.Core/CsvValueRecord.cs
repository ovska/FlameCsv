using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Runtime;

namespace FlameCsv;

/// <summary>
/// Represents the current record when reading CSV.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
[DebuggerTypeProxy(typeof(CsvValueRecord<>.CsvRecordDebugView))]
public readonly struct CsvValueRecord<T> : ICsvRecord<T> where T : unmanaged, IEquatable<T>
{
    public long Position { get; }
    public int Line { get; }

    public ReadOnlyMemory<T> RawRecord
    {
        get
        {
            _state.EnsureVersion(_version);
            return _record;
        }
    }
    public CsvDialect<T> Dialect
    {
        get
        {
            _state.EnsureVersion(_version);
            return _state.Dialect;
        }
    }

    public bool HasHeader => _state.Header is not null;

    public ReadOnlyMemory<T> this[int index] => GetField(index);
    public ReadOnlyMemory<T> this[string name] => GetField(name);

    internal int TotalFieldLength => _state.TotalFieldLength;

    internal readonly CsvEnumerationState<T> _state;
    internal readonly CsvReaderOptions<T> _options;
    internal readonly int _version;
    internal readonly RecordMeta _meta;
    private readonly ReadOnlyMemory<T> _record;

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
        _record = data;
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
            Throw.Argument_HeaderNameNotFound(name, _options.AllowContentInExceptions, _state.Header.Keys);
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
            Throw.Argument_FieldIndex(index, _state);
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
            Throw.Argument_HeaderNameNotFound(name, _options.AllowContentInExceptions, _state.Header.Keys);
        }

        return GetField<TValue>(index);
    }

    /// <inheritdoc cref="ICsvRecord{T}.GetField{TValue}(int)"/>
    public TValue GetField<TValue>(int index)
    {
        _state.EnsureVersion(_version);

        if (!_state.TryGetAtIndex(index, out ReadOnlyMemory<T> field))
        {
            Throw.Argument_FieldIndex(index, _state);
        }

        var parser = _options.GetParser<TValue>();

        if (!parser.TryParse(field.Span, out var value))
        {
            throw new UnreachableException(); // TODO
            //Throw.ParseFailed<T, TValue>(field, parser, _state);
        }

        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CsvFieldEnumerator<T> GetEnumerator() => new(value: RawRecord, state: _state, meta: _meta);

    public List<ReadOnlyMemory<T>> ToList()
    {
        List<ReadOnlyMemory<T>> list = new();

        foreach (ReadOnlyMemory<T> field in this)
        {
            list.Add(field.SafeCopy());
        }

        return list;
    }

    public TRecord ParseRecord<TRecord>()
    {
        _state.EnsureVersion(_version);

        var cache = _state.MaterializerCache;

        if (!cache.TryGetValue(typeof(TRecord), out object? cached))
        {
            cache[typeof(TRecord)] = cached = _options.GetMaterializer<T, TRecord>();
        }

        IMaterializer<T, TRecord> materializer = (IMaterializer<T, TRecord>)cached;
        CsvEnumerationStateRef<T> state = _state.GetInitialStateFor(RawRecord, _meta);

        return materializer.Parse(ref state);
    }

    private sealed class CsvRecordDebugView
    {
        private readonly CsvValueRecord<T> _record;

        public CsvRecordDebugView(CsvValueRecord<T> record) => _record = record;

        public int Line => _record.Line;
        public long Position => _record.Position;
        public string[] Headers => _record._state.Header?.Keys.ToArray() ?? Array.Empty<string>();
        public ReadOnlyMemory<T>[] Fields => _record.ToList().ToArray();
        public string[] FieldValues => Fields.Select(f => _record._options.GetAsString(f.Span)).ToArray();
    }

    public static explicit operator CsvRecord<T>(CsvValueRecord<T> record) => new(record);
}
