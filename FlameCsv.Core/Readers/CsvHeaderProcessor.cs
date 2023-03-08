using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;
using FlameCsv.Extensions;
using FlameCsv.Readers.Internal;

namespace FlameCsv.Readers;

internal struct CsvHeaderProcessor<T, TValue> : ICsvProcessor<T, TValue>
    where T : unmanaged, IEquatable<T>
{
    private readonly CsvReaderOptions<T> _options;
    private readonly IHeaderBinder<T> _binder;

    private CsvProcessor<T, TValue> _inner;
    private bool _headerRead;

    public CsvHeaderProcessor(CsvReaderOptions<T> options)
    {
        _options = options;
        _binder = options.HeaderBinder ?? HeaderMatcherDefaults.GetBinder<T>();
        _inner = default;
        _headerRead = false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryContinueRead(ref ReadOnlySequence<T> buffer, out TValue value)
    {
        if (_headerRead)
        {
            return _inner.TryContinueRead(ref buffer, out value);
        }

        return ParseHeaderAndTryRead(ref buffer, out value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadRemaining(in ReadOnlySequence<T> remaining, out TValue value)
    {
        if (_headerRead)
        {
            return _inner.TryReadRemaining(in remaining, out value);
        }

        // still read header despite it being the only line in the data
        // to ensure the data is valid for the bindings
        ReadHeader(in remaining);
        value = default!;
        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)] // encourage inlining more common paths
    private bool ParseHeaderAndTryRead(ref ReadOnlySequence<T> buffer, out TValue value)
    {
        if (LineReader.TryRead(in _options.tokens, ref buffer, out var line, out _))
        {
            ReadHeader(in line);

            Debug.Assert(_headerRead);

            return _inner.TryContinueRead(ref buffer, out value);
        }

        value = default!;
        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)] // encourage inlining more common paths
    private void ReadHeader(in ReadOnlySequence<T> line)
    {
        using var view = new SequenceView<T>(in line, _options);

        var bindings = _binder.Bind<TValue>(view.Memory.Span, _options);
        var state = _options.CreateState(bindings);
        _inner = new CsvProcessor<T, TValue>(_options, state);
        _headerRead = true;
    }

    public void Dispose()
    {
        if (_headerRead) _inner.Dispose();
    }
}
