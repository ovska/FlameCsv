using FlameCsv.Reading;
using FlameCsv.Runtime;

namespace FlameCsv.Binding;

public abstract partial class CsvTypeMap<T, TValue>
{
    private sealed class TypeMapMaterializer : IMaterializer<T, TValue>
    {
        public int FieldCount => _handlers.Length;

        private readonly Func<TValue> _valueFactory;
        private readonly TryParseHandler?[] _handlers;

        public TypeMapMaterializer(
            Func<TValue> valueFactory,
            TryParseHandler?[] handlers)
        {
            _valueFactory = valueFactory;
            _handlers = handlers;
        }

        public TValue Parse(ref CsvEnumerationStateRef<T> state)
        {
            TValue obj = _valueFactory();

            int index = 0;

            while (!state.remaining.IsEmpty)
            {
                if (index >= _handlers.Length)
                {
                    // TODO
                }

                var field = state._context.ReadNextField(ref state);
                var handler = _handlers[index++];

                if (handler is not null)
                {
                    handler(ref obj, field.Span);
                }
            }

            if (index < _handlers.Length)
            {
                // TODO
            }

            return obj;
        }

        public TValue Parse(ReadOnlySpan<ReadOnlyMemory<T>> fields, in CsvReadingContext<T> context)
        {
            if (fields.Length != FieldCount)
            {
                // TODO
            }

            TValue obj = _valueFactory();

            for (int i = 0; i < fields.Length; i++)
            {
                var handler = _handlers[i];

                if (handler is not null)
                {
                    handler(ref obj, fields[i].Span);
                }
            }

            return obj;
        }
    }
}
