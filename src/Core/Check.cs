using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace FlameCsv;

[ExcludeFromCodeCoverage]
internal static class Check
{
    [Conditional("FUZZ")]
    [Conditional("DEBUG")]
    [Obsolete("Call Check.Equal instead", true)]
    public static new void Equals(object? a, object? b) => throw new UnreachableException();

    [Conditional("FUZZ")]
    [Conditional("DEBUG")]
    [StackTraceHidden]
    public static void True(
        [DoesNotReturnIf(false)] bool condition,
        string message = "",
        [CallerArgumentExpression(nameof(condition))] string expression = ""
    )
    {
        if (!condition)
        {
            var fullMessage = string.IsNullOrEmpty(message)
                ? $"Check.True failed: {expression}"
                : $"Check.True failed: {expression} - {message}";
            throw new UnreachableException(fullMessage);
        }
    }

    [Conditional("FUZZ")]
    [Conditional("DEBUG")]
    [StackTraceHidden]
    public static void True(
        [DoesNotReturnIf(false)] bool condition,
        [InterpolatedStringHandlerArgument(nameof(condition))] ref CheckInterpolatedStringHandler message
    ) => True(condition, message.ToStringAndClear());

    [Conditional("FUZZ")]
    [Conditional("DEBUG")]
    [StackTraceHidden]
    public static void False(
        [DoesNotReturnIf(true)] bool condition,
        string message = "",
        [CallerArgumentExpression(nameof(condition))] string expression = ""
    )
    {
        if (condition)
        {
            var fullMessage = string.IsNullOrEmpty(message)
                ? $"Check.False failed: {expression}"
                : $"Check.False failed: {expression} - {message}";
            throw new UnreachableException(fullMessage);
        }
    }

    [Conditional("FUZZ")]
    [Conditional("DEBUG")]
    [StackTraceHidden]
    public static void False(
        [DoesNotReturnIf(true)] bool condition,
        [InterpolatedStringHandlerArgument(nameof(condition))] ref CheckInterpolatedStringHandler message
    ) => False(condition, message.ToStringAndClear());

    [Conditional("FUZZ")]
    [Conditional("DEBUG")]
    [StackTraceHidden]
    public static void Equal<T>(
        T value,
        T expected,
        string message = "",
        [CallerArgumentExpression(nameof(value))] string expression = ""
    )
    {
        if (!EqualityComparer<T>.Default.Equals(value, expected))
        {
            var fullMessage = string.IsNullOrEmpty(message)
                ? $"Check.Equal failed: {expression} - Expected: {expected}, Actual: {value}"
                : $"Check.Equal failed: {expression} - Expected: {expected}, Actual: {value} - {message}";
            throw new UnreachableException(fullMessage);
        }
    }

    [Conditional("FUZZ")]
    [Conditional("DEBUG")]
    [StackTraceHidden]
    public static void NotEqual<T>(
        T value,
        T notExpected,
        string message = "",
        [CallerArgumentExpression(nameof(value))] string expression = ""
    )
    {
        if (EqualityComparer<T>.Default.Equals(value, notExpected))
        {
            var fullMessage = string.IsNullOrEmpty(message)
                ? $"Check.NotEqual failed: {expression} - Not Expected: {notExpected}, Actual: {value}"
                : $"Check.NotEqual failed: {expression} - Not Expected: {notExpected}, Actual: {value} - {message}";
            throw new UnreachableException(fullMessage);
        }
    }

    [Conditional("FUZZ")]
    [Conditional("DEBUG")]
    [StackTraceHidden]
    public static void Positive<T>(
        T value,
        string message = "",
        [CallerArgumentExpression(nameof(value))] string expression = ""
    )
        where T : INumber<T>
    {
        if (value < T.Zero)
        {
            var fullMessage = string.IsNullOrEmpty(message)
                ? $"Check.Positive failed: {expression} - Value: {value}"
                : $"Check.Positive failed: {expression} - Value: {value} - {message}";
            throw new UnreachableException(fullMessage);
        }
    }

    [Conditional("FUZZ")]
    [Conditional("DEBUG")]
    [StackTraceHidden]
    public static void OverZero<T>(
        T value,
        string message = "",
        [CallerArgumentExpression(nameof(value))] string expression = ""
    )
        where T : INumber<T>
    {
        if (value <= T.Zero)
        {
            var fullMessage = string.IsNullOrEmpty(message)
                ? $"Check.OverZero failed: {expression} - Value: {value}"
                : $"Check.OverZero failed: {expression} - Value: {value} - {message}";
            throw new UnreachableException(fullMessage);
        }
    }

    [Conditional("FUZZ")]
    [Conditional("DEBUG")]
    [StackTraceHidden]
    public static void NotNull(
        [NotNull] object? value,
        string message = "",
        [CallerArgumentExpression(nameof(value))] string expression = ""
    )
    {
        if (value is null)
        {
            var fullMessage = string.IsNullOrEmpty(message)
                ? $"Check.NotNull failed: {expression}"
                : $"Check.NotNull failed: {expression} - {message}";
            throw new UnreachableException(fullMessage);
        }
    }

    [Conditional("FUZZ")]
    [Conditional("DEBUG")]
    [StackTraceHidden]
    public static void IsNull(
        object? value,
        string message = "",
        [CallerArgumentExpression(nameof(value))] string expression = ""
    )
    {
        if (value is not null)
        {
            var fullMessage = string.IsNullOrEmpty(message)
                ? $"Check.IsNull failed: {expression}"
                : $"Check.IsNull failed: {expression} - {message}";
            throw new UnreachableException(fullMessage);
        }
    }

    [Conditional("FUZZ")]
    [Conditional("DEBUG")]
    [StackTraceHidden]
    public static void GreaterThan<T>(
        T value,
        T threshold,
        string message = "",
        [CallerArgumentExpression(nameof(value))] string expression = ""
    )
        where T : IComparable<T>
    {
        if (value.CompareTo(threshold) <= 0)
        {
            var fullMessage = string.IsNullOrEmpty(message)
                ? $"Check.GreaterThan failed: {expression} - Value: {value}, Threshold: {threshold}"
                : $"Check.GreaterThan failed: {expression} - Value: {value}, Threshold: {threshold} - {message}";
            throw new UnreachableException(fullMessage);
        }
    }

    [Conditional("FUZZ")]
    [Conditional("DEBUG")]
    [StackTraceHidden]
    public static void GreaterThanOrEqual<T>(
        T value,
        T threshold,
        string message = "",
        [CallerArgumentExpression(nameof(value))] string expression = ""
    )
        where T : IComparable<T>
    {
        if (value.CompareTo(threshold) < 0)
        {
            var fullMessage = string.IsNullOrEmpty(message)
                ? $"Check.GreaterThanOrEqual failed: {expression} - Value: {value}, Threshold: {threshold}"
                : $"Check.GreaterThanOrEqual failed: {expression} - Value: {value}, Threshold: {threshold} - {message}";
            throw new UnreachableException(fullMessage);
        }
    }

    [Conditional("FUZZ")]
    [Conditional("DEBUG")]
    [StackTraceHidden]
    public static void LessThan<T>(
        T value,
        T threshold,
        string message = "",
        [CallerArgumentExpression(nameof(value))] string expression = ""
    )
        where T : IComparable<T>
    {
        if (value.CompareTo(threshold) >= 0)
        {
            var fullMessage = string.IsNullOrEmpty(message)
                ? $"Check.LessThan failed: {expression} - Value: {value}, Threshold: {threshold}"
                : $"Check.LessThan failed: {expression} - Value: {value}, Threshold: {threshold} - {message}";
            throw new UnreachableException(fullMessage);
        }
    }

    [Conditional("FUZZ")]
    [Conditional("DEBUG")]
    [StackTraceHidden]
    public static void LessThanOrEqual<T>(
        T value,
        T threshold,
        string message = "",
        [CallerArgumentExpression(nameof(value))] string expression = ""
    )
        where T : IComparable<T>
    {
        if (value.CompareTo(threshold) > 0)
        {
            var fullMessage = string.IsNullOrEmpty(message)
                ? $"Check.LessThanOrEqual failed: {expression} - Value: {value}, Threshold: {threshold}"
                : $"Check.LessThanOrEqual failed: {expression} - Value: {value}, Threshold: {threshold} - {message}";
            throw new UnreachableException(fullMessage);
        }
    }

    [InterpolatedStringHandler]
    public struct CheckInterpolatedStringHandler
    {
        private StringBuilder.AppendInterpolatedStringHandler _stringBuilderHandler;
        private readonly StringBuilder? _sb;

        public CheckInterpolatedStringHandler(
            int literalLength,
            int formattedCount,
            bool condition,
            out bool shouldAppend
        )
        {
            if (condition)
            {
                _stringBuilderHandler = default;
                shouldAppend = false;
            }
            else
            {
                // Only used when failing an assert.  Additional allocation here doesn't matter; just create a new StringBuilder.
                _stringBuilderHandler = new StringBuilder.AppendInterpolatedStringHandler(
                    literalLength,
                    formattedCount,
                    _sb = new StringBuilder()
                );
                shouldAppend = true;
            }
        }

        /// <summary>Extracts the built string from the handler.</summary>
        internal string ToStringAndClear()
        {
            string s = _sb?.ToString() ?? string.Empty;
            _stringBuilderHandler = default;
            return s;
        }

        public void AppendLiteral(string value) => _stringBuilderHandler.AppendLiteral(value);

        public void AppendFormatted<T>(T value) => _stringBuilderHandler.AppendFormatted(value);

        public void AppendFormatted<T>(T value, string? format) => _stringBuilderHandler.AppendFormatted(value, format);

        public void AppendFormatted<T>(T value, int alignment) =>
            _stringBuilderHandler.AppendFormatted(value, alignment);

        public void AppendFormatted<T>(T value, int alignment, string? format) =>
            _stringBuilderHandler.AppendFormatted(value, alignment, format);

        public void AppendFormatted(ReadOnlySpan<char> value) => _stringBuilderHandler.AppendFormatted(value);

        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) =>
            _stringBuilderHandler.AppendFormatted(value, alignment, format);

        public void AppendFormatted(string? value) => _stringBuilderHandler.AppendFormatted(value);

        public void AppendFormatted(string? value, int alignment = 0, string? format = null) =>
            _stringBuilderHandler.AppendFormatted(value, alignment, format);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) =>
            _stringBuilderHandler.AppendFormatted(value, alignment, format);
    }
}
