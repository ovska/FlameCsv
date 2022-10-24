using System.Buffers;
using System.Runtime.CompilerServices;
using FlameCsv.Binding.Providers;
using FlameCsv.Readers.Internal;

namespace FlameCsv.Readers;

internal readonly struct CsvHeaderProcessor<T, TReader, TValue> : ICsvProcessor<T, TValue>
    where T : unmanaged, IEquatable<T>
    where TReader : struct, ILineReader<T>
{
    private readonly CsvConfiguration<T> _configuration;
    private readonly ICsvHeaderBindingProvider<T> _headerBindingProvider;
    private readonly Wrapper _wrapper = new();

    public CsvHeaderProcessor(
        CsvConfiguration<T> configuration,
        ICsvHeaderBindingProvider<T> headerBindingProvider)
    {
        _configuration = configuration;
        _headerBindingProvider = headerBindingProvider;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryContinueRead(ref ReadOnlySequence<T> buffer, out TValue value)
    {
        if (_wrapper.HasValue)
        {
            return _wrapper.Value.TryContinueRead(ref buffer, out value);
        }

        Unsafe.SkipInit(out value);
        return TryReadHeader(ref buffer);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryReadHeader(ref ReadOnlySequence<T> buffer)
    {
        if (default(TReader).TryRead(in _configuration._options, ref buffer, out var line, out _))
        {
            using var view = new SequenceView<T>(line, _configuration.Security);

            if (_headerBindingProvider.TryProcessHeader(view.Span, _configuration)
                && _headerBindingProvider.TryGetBindings<TValue>(out var bindings))
            {
                _wrapper.Value = new CsvProcessor<T, TReader, TValue>(_configuration, bindings);
                _wrapper.HasValue = true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        if (_wrapper.HasValue) _wrapper.Value.Dispose();
    }

    private sealed class Wrapper
    {
        public bool HasValue;
        public CsvProcessor<T, TReader, TValue> Value;
    }
}
