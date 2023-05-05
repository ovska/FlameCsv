using CommunityToolkit.HighPerformance.Helpers;
using FlameCsv.Exceptions;
using FlameCsv.Reading;
using FlameCsv.Runtime;

namespace FlameCsv.Binding;

public abstract class CsvTypeMap<T, TValue> where T : unmanaged, IEquatable<T>
{
    protected delegate bool TryParseHandler(ref TValue value, ReadOnlySpan<T> data);

    [Flags]
    private enum BindingSupport : byte
    {
        None = 0,
        Index = 1 << 0,
        Header = 1 << 1,
        All = Index | Header,
    }

    protected abstract bool IgnoreUnparsable { get; }
    protected abstract bool IgnoreUnmatched { get; }
    protected abstract bool IgnoreDuplicate { get; }

    /// <summary>
    /// Creates an instance of <typeparamref name="TValue"/> that is hydrated from CSV records.
    /// </summary>
    protected abstract TValue CreateInstance();

    protected abstract TryParseHandler? BindMember(string name, CsvReaderOptions<T> options, ref ulong fieldMask);

    protected abstract void ValidateRequiredMembers(ICollection<string> headers, ulong fieldMask);

    internal IMaterializer<T, TValue> GetMaterializer(in CsvReadingContext<T> context)
    {
        throw new NotImplementedException();
    }

    internal IMaterializer<T, TValue> GetMaterializer(ICollection<string> headers, in CsvReadingContext<T> context)
    {
        var handlers = new TryParseHandler?[headers.Count];
        int index = 0;
        ulong fieldMask = 0;

        foreach (var header in headers)
        {
            var handler = BindMember(header, context.Options, ref fieldMask);

            if (handler is null && !IgnoreUnmatched)
            {
                throw new CsvBindingException<TValue>("TODO");
            }

            handlers[index++] = BindMember(header, context.Options, ref fieldMask);
        }

        ValidateRequiredMembers(headers, fieldMask);

        return new TypeMapMaterializer(CreateInstance, handlers);
    }

    protected static bool SetFlag(ref ulong value, byte index)
    {
        if (BitHelper.HasFlag(value, index))
            return true;

        BitHelper.SetFlag(ref value, index, true);
        return false;
    }

    protected TryParseHandler? HandleDuplicate(string member, string field, CsvReaderOptions<T> options)
    {
        if (IgnoreDuplicate)
            return null;

        throw new CsvBindingException<TValue>();
    }

    protected void ThrowRequiredNotRead(string member)
    {
    }

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
