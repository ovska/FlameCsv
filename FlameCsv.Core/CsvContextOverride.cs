using System.Buffers;
using System.Diagnostics;
using FlameCsv.Writing;

namespace FlameCsv;

public readonly struct CsvContextOverride<T> where T : unmanaged, IEquatable<T>
{
    public T Delimiter { get => _delimiter; init => _delimiter = value; }
    public T Quote { get => _quote; init => _quote = value; }
    public ReadOnlyMemory<T> Newline { get => _newline; init => _newline = value; }
    public ReadOnlyMemory<T> Whitespace { get => _whitespace; init => _whitespace = value; }
    public T? Escape { get => _escape; init => _escape = value; }
    public ArrayPool<T>? ArrayPool { get => _arrayPool; init => _arrayPool = value; }
    public bool HasHeader { get => _hasHeader; init => _hasHeader = value; }
    public bool ExposeContent { get => _exposeContent; init => _exposeContent = value; }
    public bool ValidateFieldCount { get => _validateFieldCount; init => _validateFieldCount = value; }
    public CsvExceptionHandler<T>? ExceptionHandler { get => _exceptionHandler; init => _exceptionHandler = value; }
    public CsvRecordSkipPredicate<T>? ShouldSkipRow { get => _shouldSkipRow; init => _shouldSkipRow = value; }
    public CsvFieldQuoting FieldQuoting { get => _fieldQuoting; init => _fieldQuoting = value; }

    internal readonly ValueHolder<T> _delimiter;
    internal readonly ValueHolder<T> _quote;
    internal readonly ValueHolder<ReadOnlyMemory<T>> _newline;
    internal readonly ValueHolder<ReadOnlyMemory<T>> _whitespace;
    internal readonly ValueHolder<T?> _escape;
    internal readonly ValueHolder<ArrayPool<T>?> _arrayPool;
    internal readonly ValueHolder<bool> _hasHeader;
    internal readonly ValueHolder<bool> _exposeContent;
    internal readonly ValueHolder<bool> _validateFieldCount;
    internal readonly ValueHolder<CsvExceptionHandler<T>?> _exceptionHandler;
    internal readonly ValueHolder<CsvRecordSkipPredicate<T>?> _shouldSkipRow;
    internal readonly ValueHolder<CsvFieldQuoting> _fieldQuoting;

    [DebuggerDisplay("{DebugString,nq}")]
    internal readonly struct ValueHolder<TValue>(TValue value)
    {
        private readonly TValue _value = value;
        private readonly bool _hasValue = true;

        public static implicit operator ValueHolder<TValue>(TValue value) => new(value);
        public static implicit operator TValue(ValueHolder<TValue> holder) => holder._value;

        public TValue Resolve(TValue defaultValue) => _hasValue ? _value : defaultValue;

        internal string DebugString => $"ValueHolder: {(_hasValue ? $"Value: {_value}" : "No value")}";
    }
}
