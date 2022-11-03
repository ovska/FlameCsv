using FlameCsv.Binding;

namespace FlameCsv;

/// <summary>
/// Callback for a CSV row or column.
/// </summary>
/// <param name="data">Row or column tokens</param>
/// <param name="tokens">Current operation's options</param>
/// <typeparam name="T">CSV token type</typeparam>
/// <typeparam name="TReturn">Return value</typeparam>
public delegate TReturn CsvCallback<T, out TReturn>(
    ReadOnlySpan<T> data,
    in CsvTokens<T> tokens)
    where T : unmanaged, IEquatable<T>;

/// <summary>
/// Parse callback for a CSV column.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
/// <typeparam name="TValue">Return value</typeparam>
public delegate bool CsvTryParse<T, TValue>(
    ReadOnlySpan<T> data,
    out TValue value)
    where T : unmanaged, IEquatable<T>;

/// <summary>
/// Callback for matching CSV header columns to members.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public delegate CsvBinding? CsvHeaderMatcher<T>(
    in HeaderBindingArgs args,
    ReadOnlySpan<T> data);
