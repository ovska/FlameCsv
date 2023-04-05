using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Binding;
using FlameCsv.Extensions;

namespace FlameCsv.Reading;

internal struct CsvHeaderProcessor<T, TValue> : ICsvProcessor<T, TValue>
    where T : unmanaged, IEquatable<T>
{
    private readonly CsvReaderOptions<T> _options;
    private readonly IHeaderBinder<T> _binder;

    private CsvProcessor<T, TValue>? _inner;

    public CsvHeaderProcessor(CsvReaderOptions<T> options)
    {
        _options = options;
        _binder = options.HeaderBinder ?? HeaderMatcherDefaults.GetBinder<T>();
        _inner = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRead(ref ReadOnlySequence<T> buffer, out TValue value, bool isFinalBlock)
    {
        if (_inner.HasValue)
        {
            ref var inner = ref _inner.DangerousGetValueOrDefaultReference();
            return inner.TryRead(ref buffer, out value, isFinalBlock);
        }

        return ParseHeaderAndTryRead(ref buffer, out value, isFinalBlock);
    }

    [MethodImpl(MethodImplOptions.NoInlining)] // encourage inlining more common paths
    private bool ParseHeaderAndTryRead(ref ReadOnlySequence<T> buffer, out TValue value, bool isFinalBlock)
    {
        if (LineReader.TryGetLine(new CsvDialect<T>(_options), ref buffer, out var line, out _, isFinalBlock))
        {
            ReadHeader(in line);

            if (!isFinalBlock)
            {
                ref var inner = ref _inner.DangerousGetValueOrDefaultReference();
                return inner.TryRead(ref buffer, out value, isFinalBlock);
            }
        }

        value = default!;
        return false;
    }

    [MemberNotNull(nameof(_inner))]
    [MethodImpl(MethodImplOptions.NoInlining)] // encourage inlining more common paths
    private void ReadHeader(in ReadOnlySequence<T> line)
    {
        using var view = new SequenceView<T>(in line, _options);

        var bindings = _binder.Bind<TValue>(view.Memory.Span, _options);
        var materializer = _options.CreateMaterializerFrom(bindings);
        _inner = new CsvProcessor<T, TValue>(_options, materializer);
    }

    public void Dispose()
    {
        if (_inner.HasValue)
        {
            ref var inner = ref _inner.DangerousGetValueOrDefaultReference();
            inner.Dispose();
        }
    }
}
