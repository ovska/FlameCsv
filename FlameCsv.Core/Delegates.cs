using System.Diagnostics.CodeAnalysis;
using FlameCsv.Binding;

namespace FlameCsv;

/// <summary>
/// Callback for a CSV row or column.
/// </summary>
/// <param name="data">The tokens, may be a trimmed column or a whole row</param>
/// <param name="tokens">Current operation's CSV tokens</param>
/// <typeparam name="T">CSV token type</typeparam>
/// <typeparam name="TReturn">Return value</typeparam>
public delegate TReturn CsvCallback<T, out TReturn>(
    ReadOnlySpan<T> data,
    in CsvTokens<T> tokens)
    where T : unmanaged, IEquatable<T>;

/// <summary>
/// Callback for matching CSV header columns to members.
/// </summary>
/// <param name="column">Trimmed column value</param>
/// <param name="args">Possible match for the header</param>
/// <typeparam name="T">Token type</typeparam>
public delegate CsvBinding? CsvHeaderMatcher<T>(
    ReadOnlySpan<T> column,
    in HeaderBindingArgs args);

/// <summary>
/// Callback for custom handling of parsing errors.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
/// <param name="data">Data being parsed</param>
/// <param name="exception">Thrown exception</param>
/// <returns><see langword="true"/> if the exception can be ignored.</returns>
public delegate bool CsvExceptionHandler<T>(
    ReadOnlySpan<T> data,
    Exception exception);

internal delegate bool SpanPredicate<T>(ReadOnlySpan<T> data);
