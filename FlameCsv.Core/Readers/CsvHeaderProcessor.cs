using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using FlameCsv.Binding.Providers;
using FlameCsv.Exceptions;
using FlameCsv.Readers.Internal;

namespace FlameCsv.Readers;

internal readonly struct CsvHeaderProcessor<T, TValue> : ICsvProcessor<T, TValue>
    where T : unmanaged, IEquatable<T>
{
    private readonly CsvReaderOptions<T> _options;
    private readonly ICsvHeaderBindingProvider<T> _headerBindingProvider;
    private readonly Wrapper _wrapper = new();

    public CsvHeaderProcessor(CsvReaderOptions<T> options)
    {
        _options = options;
        _headerBindingProvider = (ICsvHeaderBindingProvider<T>)_options.BindingProvider;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryContinueRead(ref ReadOnlySequence<T> buffer, out TValue value)
    {
        if (_wrapper.HasValue)
        {
            return _wrapper.Value.TryContinueRead(ref buffer, out value);
        }

        return ParseHeaderAndTryRead(ref buffer, out value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadRemaining(in ReadOnlySequence<T> remaining, out TValue value)
    {
        if (_wrapper.HasValue)
        {
            return _wrapper.Value.TryReadRemaining(in remaining, out value);
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

            Debug.Assert(_wrapper.HasValue);

            return _wrapper.Value.TryContinueRead(ref buffer, out value);
        }

        value = default!;
        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)] // encourage inlining more common paths
    private void ReadHeader(in ReadOnlySequence<T> line)
    {
        using var view = new SequenceView<T>(in line, _options);

        if (_headerBindingProvider.TryProcessHeader(view.Memory.Span, _options)
            && _headerBindingProvider.TryGetBindings<TValue>(out var bindings))
        {
            var state = _options.CreateState(bindings);
            _wrapper.Value = new CsvProcessor<T, TValue>(_options, state);
            _wrapper.HasValue = true;
        }
        else
        {
            throw new CsvBindingException("TODO");
        }
    }

    public void Dispose()
    {
        if (_wrapper.HasValue) _wrapper.Value.Dispose();
    }

    // mutable wrapper so the processor can be readonly
    private sealed class Wrapper
    {
        public bool HasValue;
        public CsvProcessor<T, TValue> Value;
    }
}
