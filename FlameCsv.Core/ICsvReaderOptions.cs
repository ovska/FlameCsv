using System.Buffers;
using FlameCsv.Writers;

namespace FlameCsv;

public readonly struct CsvContextOverride<T> where T : unmanaged, IEquatable<T>
{
    public T Delimiter { get => _delimiter; init => _delimiter = value; }
    public T Quote { get => _quote; init => _quote = value; }
    public ReadOnlyMemory<T> Newline { get => _newline; init => _newline = value; }
    public T? Escape { get => _escape; init => _escape = value; }
    public ArrayPool<T>? ArrayPool { get => _arrayPool; init => _arrayPool = value; }
    public bool HasHeader { get => _hasHeader; init => _hasHeader = value; }
    public bool ExposeContent { get => _exposeContent; init => _exposeContent = value; }
    public bool ValidateFieldCount { get => _validateFieldCount; init => _validateFieldCount = value; }
    public CsvExceptionHandler<T>? ExceptionHandler { get => _exceptionHandler; init => _exceptionHandler = value; }
    public CsvCallback<T, bool>? ShouldSkipRow { get => _shouldSkipRow; init => _shouldSkipRow = value; }

    internal readonly ValueHolder<T> _delimiter;
    internal readonly ValueHolder<T> _quote;
    internal readonly ValueHolder<ReadOnlyMemory<T>> _newline;
    internal readonly ValueHolder<T?> _escape;
    internal readonly ValueHolder<ArrayPool<T>?> _arrayPool;
    internal readonly ValueHolder<bool> _hasHeader;
    internal readonly ValueHolder<bool> _exposeContent;
    internal readonly ValueHolder<bool> _validateFieldCount;
    internal readonly ValueHolder<CsvExceptionHandler<T>?> _exceptionHandler;
    internal readonly ValueHolder<CsvCallback<T, bool>?> _shouldSkipRow;

    internal readonly struct ValueHolder<TValue>
    {
        private readonly TValue _value;
        private readonly bool _hasValue;

        public ValueHolder(TValue value)
        {
            _value = value;
            _hasValue = true;
        }

        public static implicit operator ValueHolder<TValue>(TValue value) => new(value);
        public static implicit operator TValue(ValueHolder<TValue> holder) => holder._value;

        public TValue Resolve(TValue defaultValue) => _hasValue ? _value : defaultValue;
    }
}

public interface ICsvOptions<T> where T : unmanaged, IEquatable<T>
{
    bool AllowContentInExceptions { get; }

    ArrayPool<T>? ArrayPool { get; }
    bool HasHeader { get; }
    IDictionary<Type, string?> NullTokens { get; }
    CsvExceptionHandler<T>? ExceptionHandler { get; }

    string GetAsString(ReadOnlySpan<T> field);
    ReadOnlyMemory<T> GetNullToken(Type resultType);
}

public interface ICsvWriterOptions<T> where T : unmanaged, IEquatable<T>
{
    bool IsReadOnly { get; }
    bool MakeReadOnly();

    CsvFieldQuoting FieldQuoting { get; }
}

public interface ICsvReaderOptions<T> where T : unmanaged, IEquatable<T>
{
    bool IsReadOnly { get; }
    bool MakeReadOnly();

    StringComparison Comparison { get; }
    CsvCallback<T, bool>? ShouldSkipRow { get; }
    bool ValidateFieldCount { get; }
}
