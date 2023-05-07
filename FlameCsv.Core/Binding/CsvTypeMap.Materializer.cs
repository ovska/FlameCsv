using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Runtime;

namespace FlameCsv.Binding;

public abstract partial class CsvTypeMap<T, TValue>
{
    protected sealed class TypeMapMaterializer<TState> : IMaterializer<T, TValue>
        where TState : struct, ITypeMapState
    {
        public int FieldCount => _state.Count;

        private readonly Func<TValue> _valueFactory;
        private TState _state;

        public TypeMapMaterializer(
            Func<TValue> valueFactory,
            TState state)
        {
            _valueFactory = valueFactory;
            _state = state;
        }

        TValue IMaterializer<T, TValue>.Parse(ref CsvEnumerationStateRef<T> state)
        {
            TValue obj = _valueFactory();

            int index = 0;

            while (!state.remaining.IsEmpty)
            {
                if (index >= _state.Count)
                {
                    Throw.InvalidData_FieldCount();
                }

                ReadOnlySpan<T> field = state._context.ReadNextField(ref state).Span;

                if (_state.TryParse(index++, ref obj, field))
                    continue;

                state.ThrowParseFailed(field, null);
            }

            if (index < FieldCount)
            {
                state.ThrowRecordEndedPrematurely(FieldCount, typeof(TValue));
            }

            return obj;
        }

        TValue IMaterializer<T, TValue>.Parse(ReadOnlySpan<ReadOnlyMemory<T>> fields, in CsvReadingContext<T> context)
        {
            if (fields.Length != FieldCount)
            {
                Throw.InvalidData_FieldCount(FieldCount, fields.Length);
            }

            TValue obj = _valueFactory();

            for (int i = 0; i < fields.Length; i++)
            {
                if (!_state.TryParse(i, ref obj, fields[i].Span))
                {
                    Throw.ParseFailed(fields[i], typeof(TValue), in context);
                }
            }

            return obj;
        }
    }
}
