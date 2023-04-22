using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;

namespace FlameCsv.Reading;

internal struct CsvHeaderProcessor<T, TValue> : ICsvProcessor<T, TValue>
    where T : unmanaged, IEquatable<T>
{
    private readonly CsvReaderOptions<T> _options;

    private CsvProcessor<T, TValue>? _inner;

    public CsvHeaderProcessor(CsvReaderOptions<T> options)
    {
        _options = options;
        _inner = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRead(ref ReadOnlySequence<T> buffer, out TValue value, bool isFinalBlock)
    {
        if (_inner.HasValue)
        {
            return _inner.DangerousGetValueOrDefaultReference().TryRead(ref buffer, out value, isFinalBlock);
        }

        return ParseHeaderAndTryRead(ref buffer, out value, isFinalBlock);
    }

    [MethodImpl(MethodImplOptions.NoInlining)] // encourage inlining more common paths
    private bool ParseHeaderAndTryRead(ref ReadOnlySequence<T> buffer, out TValue value, bool isFinalBlock)
    {
        if (new CsvDialect<T>(_options).TryGetLine(ref buffer, out var line, out _, isFinalBlock))
        {
            ReadHeader(in line);

            Debug.Assert(_inner.HasValue);

            if (!isFinalBlock)
            {
                return _inner.DangerousGetValueOrDefaultReference().TryRead(ref buffer, out value, isFinalBlock);
            }
        }

        value = default!;
        return false;
    }

    [MemberNotNull(nameof(_inner))]
    private void ReadHeader(in ReadOnlySequence<T> line)
    {
        using var view = new SequenceView<T>(in line, _options.ArrayPool);

        var headerBinder = _options.GetHeaderBinder();
        var bindings = headerBinder.Bind<TValue>(view.Memory);
        var materializer = _options.CreateMaterializerFrom(bindings);
        _inner = new CsvProcessor<T, TValue>(_options, materializer);
    }

    public void Dispose()
    {
        if (_inner.HasValue)
        {
            _inner.Value.Dispose();
        }
    }
}
