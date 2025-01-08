using CommunityToolkit.HighPerformance;
using FlameCsv.Binding;
using FlameCsv.Extensions;
using FlameCsv.Writing;

namespace FlameCsv.Runtime;

internal abstract class Dematerializer<T, TValue> : Dematerializer<T> where T : unmanaged, IBinaryInteger<T>
{
    private readonly CsvBindingCollection<TValue> _bindings;

    protected Dematerializer(CsvBindingCollection<TValue> bindings)
    {
        _bindings = bindings;
    }

    public void WriteHeader(ref readonly CsvFieldWriter<T> writer)
    {
        foreach (var item in _bindings.MemberBindings.Enumerate())
        {
            if (item.Index != 0)
                writer.WriteDelimiter();

            if (item.Value.Header is null)
                Throw.InvalidOp_NoHeader(item.Index, typeof(TValue), item.Value.Member);

            writer.WriteText(item.Value.Header);
        }

        writer.WriteNewline();
    }
}
