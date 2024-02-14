using CommunityToolkit.Diagnostics;
using FlameCsv.Binding;
using FlameCsv.Writing;

namespace FlameCsv.Runtime;

internal abstract class Dematerializer<T, TValue> : Dematerializer<T> where T : unmanaged, IEquatable<T>
{
    private readonly CsvBindingCollection<TValue> _bindings;

    protected Dematerializer(CsvBindingCollection<TValue> bindings)
    {
        _bindings = bindings;
    }

    public void WriteHeader(ICsvFieldWriter<T> writer)
    {
        writer.WriteText(_bindings.MemberBindings[0].Header);

        for (int i = 1; i < _bindings.MemberBindings.Length; i++)
        {
            writer.WriteDelimiter();

            if (_bindings.MemberBindings[i].Header is null)
            {
                ThrowHelper.ThrowInvalidOperationException(
                    $"No header name found for column index {i} when writing {typeof(TValue).FullName}");
            }

            writer.WriteText(_bindings.MemberBindings[i].Header);
        }

        writer.WriteNewline();
    }
}
