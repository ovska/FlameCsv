namespace FlameCsv;



//todo: public api description
public readonly partial struct CsvValueRecord<T> : IReadOnlyList<ReadOnlyMemory<T>>, IList<ReadOnlyMemory<T>>
{
    ReadOnlyMemory<T> IReadOnlyList<ReadOnlyMemory<T>>.this[int index] => this[index];
    ReadOnlyMemory<T> IList<ReadOnlyMemory<T>>.this[int index]
    {
        get => this[index];
        set => throw new NotSupportedException();
    }

    int IReadOnlyCollection<ReadOnlyMemory<T>>.Count => GetFieldCount();
    int ICollection<ReadOnlyMemory<T>>.Count => GetFieldCount();
    bool ICollection<ReadOnlyMemory<T>>.IsReadOnly => true;
    void ICollection<ReadOnlyMemory<T>>.Add(ReadOnlyMemory<T> item) => throw new NotSupportedException();
    void ICollection<ReadOnlyMemory<T>>.Clear() => throw new NotSupportedException();
    void ICollection<ReadOnlyMemory<T>>.CopyTo(ReadOnlyMemory<T>[] array, int arrayIndex) => throw new NotSupportedException();
    void IList<ReadOnlyMemory<T>>.Insert(int index, ReadOnlyMemory<T> item) => throw new NotSupportedException();
    bool ICollection<ReadOnlyMemory<T>>.Remove(ReadOnlyMemory<T> item) => throw new NotSupportedException();
    void IList<ReadOnlyMemory<T>>.RemoveAt(int index) => throw new NotSupportedException();

    bool ICollection<ReadOnlyMemory<T>>.Contains(ReadOnlyMemory<T> item) => ((IList<ReadOnlyMemory<T>>)this).IndexOf(item) >= 0;

    int IList<ReadOnlyMemory<T>>.IndexOf(ReadOnlyMemory<T> item)
    {
        int index = 0;

        foreach (var field in this)
        {
            if (item.Span.SequenceEqual(field.Span))
                return index;

            index++;
        }

        return -1;
    }
}
