using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;
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

    /// <summary>
    /// Whether the current CSV enumeration has a header.
    /// </summary>
    public bool HasHeader => _state.Header is not null;

    /// <summary>
    /// Whether the record has no fields.
    /// </summary>
    public bool IsEmpty => _record.IsEmpty;

    public ReadOnlyMemory<T> this[int index] => GetField(index);
    public ReadOnlyMemory<T> this[string name] => GetField(name);

    internal readonly EnumeratorState<T> _state;
    internal readonly CsvOptions<T> _options;

    private readonly int _version;
    private readonly ReadOnlyMemory<T> _record;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvValueRecord(
        long position,
        int line,
        ReadOnlyMemory<T> data,
        CsvOptions<T> options,
        RecordMeta meta,
        EnumeratorState<T> state)
    {
        Position = position;
        Line = line;
        _record = data;
        _options = options;
        _state = state;
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

    public bool TryGetValue<TValue>(int index, [MaybeNullWhen(false)] out TValue value)
    {
        _state.EnsureVersion(_version);

        if (!_state.TryGetAtIndex(index, out ReadOnlyMemory<T> field))
        {
            Throw.Argument_FieldIndex(index, _state);
        }

        if (!_options.GetConverter<TValue>().TryParse(field.Span, out value))
        {
            value = default;
            return false;
        }

        return true;
    }

    public bool TryGetValue<TValue>(string name, [MaybeNullWhen(false)] out TValue value)
    {
        _state.EnsureVersion(_version);

        if (!_state.TryGetHeaderIndex(name, out int index))
        {
            Throw.Argument_HeaderNameNotFound(name, _state._context.ExposeContent, _state.Header.Keys);
        }

        return TryGetValue(index, out value);
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

        var parser = _options.GetConverter<TValue>();

        if (!parser.TryParse(field.Span, out var value))
        {
            Throw.ParseFailed<T, TValue>(field, parser, in _state._context);
        }

        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetHeader() => _state.Header = null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(_version, _state);

    public List<ReadOnlyMemory<T>> ToList()
    {
        List<ReadOnlyMemory<T>> list = new();

        foreach (ReadOnlyMemory<T> field in this)
        {
            list.Add(field.SafeCopy());
        }

        return list;
    }

    [RequiresUnreferencedCode(Messages.CompiledExpressions)]
    public TRecord ParseRecord<[DynamicallyAccessedMembers(Messages.ReflectionBound)] TRecord>()
    {
        _state.EnsureVersion(_version);


        if (!_state.MaterializerCache.TryGetValue(typeof(TRecord), out object? obj))
        {
            var headers = _state._headerNames;

            if (headers is not null)
            {
                var bindings = _options.GetHeaderBinder().Bind<TRecord>(headers);
                obj = _state._context.Options.CreateMaterializerFrom(bindings);
            }
            else
            {
                obj = _options.GetMaterializer<T, TRecord>();
            }

            _state.MaterializerCache[typeof(TRecord)] = obj;
        }

        IMaterializer<T, TRecord> materializer = (IMaterializer<T, TRecord>)obj;
        CsvRecordFieldReader<T> reader = new(_state.GetFields(), in _state._context);
        return materializer.Parse(ref reader);
    }

    public TRecord ParseRecord<TRecord>(CsvTypeMap<T, TRecord> typeMap)
    {
        ArgumentNullException.ThrowIfNull(typeMap);

        if (!_state.MaterializerCache.TryGetValue(typeMap, out object? obj))
        {
            obj = _state._headerNames is not null
                ? typeMap.GetMaterializer(_state._headerNames, in _state._context)
                : typeMap.GetMaterializer(in _state._context);

            _state.MaterializerCache[typeMap] = obj;
        }

        IMaterializer<T, TRecord> materializer = (IMaterializer<T, TRecord>)obj;
        CsvRecordFieldReader<T> reader = new(_state.GetFields(), in _state._context);
        return materializer.Parse(ref reader);
    }

    public struct Enumerator
    {
        public ReadOnlyMemory<T> Current { get; private set; }

        private readonly int _version;
        private readonly ArraySegment<ReadOnlyMemory<T>> _fields;
        private readonly EnumeratorState<T> _state;
        private int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(int version, EnumeratorState<T> state)
        {
            state.EnsureVersion(version);
            state.FullyConsume();

            _version = version;
            _state = state;
            _fields = state.GetFields();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            _state.EnsureVersion(_version);

            if (_index < _fields.Count)
            {
                Current = _fields[_index++];
                return true;
            }

            Current = default;
            return false;
        }
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
