using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;
using FlameCsv.Extensions;
using FlameCsv.Runtime;
using FlameCsv.Writing;
using DAM = System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute;
using RUF = System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute;

namespace FlameCsv;

internal sealed class CsvWriterImpl<T, TWriter> : CsvWriter<T>
    where T : unmanaged, IEquatable<T>
    where TWriter : struct, ICsvBufferWriter<T>
{
    private readonly CsvOptions<T> _options;
    private readonly CsvFieldWriter<T, TWriter> _inner;
    private readonly bool _autoFlush;

    private readonly Dictionary<object, object> _dematerializerCache = [];
    private object? _previousKey;
    private object? _previousValue;

    private int _index;
    private int _line;
    private int? _fieldCount;

    public override int ColumnIndex => _index;
    public override int LineIndex => _line;

    public CsvWriterImpl(
        CsvOptions<T> options,
        CsvFieldWriter<T, TWriter> inner,
        bool autoFlush)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(inner);

        options.MakeReadOnly();
        _options = options;
        _inner = inner;
        _autoFlush = autoFlush;
        _line = 1;
    }

    public override void WriteField<TField>([AllowNull] TField value)
    {
        WriteFieldCore(value);
        FlushIfNeeded();
    }

    public override ValueTask WriteFieldAsync<TField>([AllowNull] TField value, CancellationToken cancellationToken)
    {
        WriteFieldCore(value);
        return FlushIfNeededAsync(cancellationToken);
    }

    public override void WriteField(ReadOnlySpan<char> text, bool skipEscaping = false)
    {
        WriteFieldCore(text);
        FlushIfNeeded();
    }

    public override ValueTask WriteFieldAsync(ReadOnlySpan<char> text, bool skipEscaping = false, CancellationToken cancellationToken = default)
    {
        WriteFieldCore(text);
        return FlushIfNeededAsync(cancellationToken);
    }

    public override void NextRecord()
    {
        if (_options._validateFieldCount &&
            _fieldCount.HasValue &&
            _index != _fieldCount.Value)
        {
            Throw.InvalidData_FieldCount(_fieldCount.Value, _index);
        }

        _inner.WriteNewline();
        _fieldCount ??= _index;
        _index = 0;
        _line++;
        FlushIfNeeded();
    }

    public override ValueTask NextRecordAsync(CancellationToken cancellationToken = default)
    {
        if (_options._validateFieldCount &&
            _fieldCount.HasValue &&
            _index != _fieldCount.Value)
        {
            return Throw.InvalidData_FieldCountAsync(_fieldCount.Value, _index);
        }

        _inner.WriteNewline();
        _fieldCount ??= _index;
        _index = 0;
        _line++;
        return FlushIfNeededAsync(cancellationToken);
    }

    public override void Dispose() => _inner.Writer.Complete(null);

    public override ValueTask DisposeAsync() => _inner.Writer.CompleteAsync(null);

    public override void Flush() => _inner.Writer.Flush();
    public override ValueTask FlushAsync(CancellationToken cancellationToken = default) => _inner.Writer.FlushAsync(cancellationToken);

    private void WriteFieldCore<TField>([AllowNull] TField value)
    {
        WriteDelimiterIfNeeded();
        _inner.WriteField(_options.GetConverter<TField?>(), value);
        _index++;
    }

    private void WriteFieldCore(ReadOnlySpan<char> text, bool skipEscaping = false)
    {
        WriteDelimiterIfNeeded();
        _inner.WriteText(text, skipEscaping);
        _index++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ValueTask FlushIfNeededAsync(CancellationToken cancellationToken)
    {
        if (_autoFlush && _inner.Writer.NeedsFlush)
            return _inner.Writer.FlushAsync(cancellationToken);

        return !cancellationToken.IsCancellationRequested ? default : ValueTask.FromCanceled(cancellationToken);
    }

    [RUF(Messages.CompiledExpressions)]
    public override void WriteRecord<[DAM(Messages.ReflectionBound)] TRecord>(TRecord value)
    {
        WriteDelimiterIfNeeded();
        GetDematerializerAndIncrementFieldCount<TRecord>().Write(_inner, value);
        FlushIfNeeded();
    }

    public override void WriteRecord<TRecord>(CsvTypeMap<T, TRecord> typeMap, TRecord value)
    {
        WriteDelimiterIfNeeded();
        GetDematerializerAndIncrementFieldCount(typeMap).Write(_inner, value);
        FlushIfNeeded();
    }

    public override void WriteRaw(ReadOnlySpan<T> value)
    {
        WriteRawCore(value);
        FlushIfNeeded();
    }

    public override ValueTask WriteRawAsync(ReadOnlySpan<T> value, CancellationToken cancellationToken = default)
    {
        WriteRawCore(value);
        return FlushIfNeededAsync(cancellationToken);
    }

    private void WriteRawCore(scoped ReadOnlySpan<T> value)
    {
        Span<T> destination = _inner.Writer.GetSpan(value.Length);
        value.CopyTo(destination);
        _inner.Writer.Advance(value.Length);
    }

    [RUF(Messages.CompiledExpressions)]
    public override ValueTask WriteRecordAsync<[DAM(Messages.ReflectionBound)] TRecord>(TRecord value, CancellationToken cancellationToken = default)
    {
        WriteDelimiterIfNeeded();
        GetDematerializerAndIncrementFieldCount<TRecord>().Write(_inner, value);
        return FlushIfNeededAsync(cancellationToken);
    }

    public override ValueTask WriteRecordAsync<TRecord>(CsvTypeMap<T, TRecord> typeMap, TRecord value, CancellationToken cancellationToken = default)
    {
        WriteDelimiterIfNeeded();
        GetDematerializerAndIncrementFieldCount(typeMap).Write(_inner, value);
        return FlushIfNeededAsync(cancellationToken);
    }

    [RUF(Messages.CompiledExpressions)]
    public override void WriteHeader<[DAM(Messages.ReflectionBound)] TRecord>()
    {
        WriteDelimiterIfNeeded();
        GetDematerializerAndIncrementFieldCount<TRecord>().WriteHeader(_inner);
        FlushIfNeeded();
    }

    public override void WriteHeader<TRecord>(CsvTypeMap<T, TRecord> typeMap)
    {
        WriteDelimiterIfNeeded();
        GetDematerializerAndIncrementFieldCount(typeMap).WriteHeader(_inner);
        FlushIfNeeded();
    }

    [RUF(Messages.CompiledExpressions)]
    public override ValueTask WriteHeaderAsync<[DAM(Messages.ReflectionBound)] TRecord>(CancellationToken cancellationToken = default)
    {
        WriteDelimiterIfNeeded();
        GetDematerializerAndIncrementFieldCount<TRecord>().WriteHeader(_inner);
        return FlushIfNeededAsync(cancellationToken);
    }

    public override ValueTask WriteHeaderAsync<TRecord>(CsvTypeMap<T, TRecord> typeMap, CancellationToken cancellationToken = default)
    {
        WriteDelimiterIfNeeded();
        GetDematerializerAndIncrementFieldCount(typeMap).WriteHeader(_inner);
        return FlushIfNeededAsync(cancellationToken);
    }

    private IDematerializer<T, TRecord> GetDematerializerAndIncrementFieldCount<TRecord>(CsvTypeMap<T, TRecord> typeMap)
    {
        if (ReferenceEquals(_previousKey, typeMap))
            return (IDematerializer<T, TRecord>)_previousValue!;

        IDematerializer<T, TRecord> dematerializer;

        if (!_dematerializerCache.TryGetValue(typeMap, out object? cached))
        {
            _dematerializerCache[typeMap] = dematerializer = typeMap.GetDematerializer(_options);
        }
        else
        {
            dematerializer = (IDematerializer<T, TRecord>)cached;
        }

        _previousKey = typeMap;
        _previousValue = dematerializer;

        _index += dematerializer.FieldCount;
        return dematerializer;
    }

    [RUF(Messages.CompiledExpressions)]
    private IDematerializer<T, TRecord> GetDematerializerAndIncrementFieldCount<[DAM(Messages.ReflectionBound)] TRecord>()
    {
        if (ReferenceEquals(_previousKey, typeof(TRecord)))
            return (IDematerializer<T, TRecord>)_previousValue!;

        IDematerializer<T, TRecord> dematerializer;

        if (!_dematerializerCache.TryGetValue(typeof(TRecord), out object? cached))
        {
            _dematerializerCache[typeof(TRecord)] = dematerializer = ReflectionDematerializer.Create<T, TRecord>(_options);
        }
        else
        {
            dematerializer = (IDematerializer<T, TRecord>)cached;
        }

        _previousKey = typeof(TRecord);
        _previousValue = dematerializer;

        _index += dematerializer.FieldCount;
        return dematerializer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteDelimiterIfNeeded()
    {
        if (_index > 0)
            _inner.WriteDelimiter();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FlushIfNeeded()
    {
        if (_autoFlush && _inner.Writer.NeedsFlush)
            _inner.Writer.Flush();
    }
}
