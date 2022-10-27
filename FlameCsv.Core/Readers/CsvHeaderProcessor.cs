using System.Buffers;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using FlameCsv.Binding.Providers;
using FlameCsv.Readers.Internal;

namespace FlameCsv.Readers;

internal readonly struct CsvHeaderProcessor<T, TValue> : ICsvProcessor<T, TValue>
    where T : unmanaged, IEquatable<T>
{
    private readonly CsvConfiguration<T> _configuration;
    private readonly ICsvHeaderBindingProvider<T> _headerBindingProvider;
    private readonly Wrapper _wrapper = new();

    public CsvHeaderProcessor(CsvConfiguration<T> configuration)
    {
        _configuration = configuration;
        _headerBindingProvider = (ICsvHeaderBindingProvider<T>)_configuration.BindingProvider;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryContinueRead(ref ReadOnlySequence<T> buffer, out TValue value)
    {
        if (_wrapper.HasValue)
        {
            return _wrapper.Value.TryContinueRead(ref buffer, out value);
        }

        return TryReadHeader(ref buffer, out value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryReadHeader(ref ReadOnlySequence<T> buffer, out TValue value)
    {
        if (LineReader.TryRead(in _configuration._options, ref buffer, out var line, out _))
        {
            using var view = new SequenceView<T>(line, _configuration.Security);

            if (_headerBindingProvider.TryProcessHeader(view.Span, _configuration)
                && _headerBindingProvider.TryGetBindings<TValue>(out var bindings))
            {
                var state = _configuration.CreateState(bindings);
                _wrapper.Value = new CsvProcessor<T, TValue>(_configuration, state);
                _wrapper.HasValue = true;
            }
            else
            {
                ThrowHelper.ThrowInvalidOperationException();
            }
        }

        Unsafe.SkipInit(out value);
        return false;
    }

    public void Dispose()
    {
        if (_wrapper.HasValue) _wrapper.Value.Dispose();
    }

    private sealed class Wrapper
    {
        public bool HasValue;
        public CsvProcessor<T, TValue> Value;
    }
}
