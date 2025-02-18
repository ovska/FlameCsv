using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;
using FlameCsv.Reading;
using JetBrains.Annotations;

namespace FlameCsv.Parallel;

internal readonly struct ValueParallelInvoke<T, TValue>
    : ICsvParallelTryInvoke<T, TValue>
    where T : unmanaged, IBinaryInteger<T>
{
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    public static ValueParallelInvoke<T, TValue> Create(CsvOptions<T> options)
    {
        return new(
            new(options, null),
            static (arg, header) => header is null
                ? arg.Options.TypeBinder.GetMaterializer<TValue>()
                : arg.Options.TypeBinder.GetMaterializer<TValue>(header.Values));
    }

    public static ValueParallelInvoke<T, TValue> Create(CsvOptions<T> options, CsvTypeMap<T, TValue> typeMap)
    {
        return new(
            new(options, typeMap),
            static (state, header) => header is null
                ? state.TypeMap!.GetMaterializer(state.Options)
                : state.TypeMap!.GetMaterializer(header.Values, state.Options));
    }

    private readonly StrongBox<IMaterializer<T, TValue>> _materializer = new();
    private readonly Arg _arg;
    private readonly Func<Arg, CsvHeader?, IMaterializer<T, TValue>> _materializerFactory;

    private ValueParallelInvoke(
        Arg arg,
        [RequireStaticDelegate] Func<Arg, CsvHeader?, IMaterializer<T, TValue>> materializerFactory)
    {
        _arg = arg;
        _materializerFactory = materializerFactory;
    }

    public bool TryInvoke<TRecord>(
        scoped ref TRecord record,
        in CsvParallelState state,
        [NotNullWhen(true)] out TValue? result) where TRecord : ICsvFields<T>, allows ref struct
    {
        result = (_materializer.Value ??= _materializerFactory(_arg, state.Header)).Parse(ref record)!;
        return true;
    }

    private readonly record struct Arg(CsvOptions<T> Options, CsvTypeMap<T, TValue>? TypeMap);
}
