using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using FlameCsv.Exceptions;

namespace FlameCsv;

public readonly partial record struct CsvTokens<T>
{
    /// <summary>
    /// Returns possible validation errors in the tokens instance.
    /// </summary>
    /// <param name="errors">Non-null non-empty list of errors if options are invalid</param>
    /// <returns>True if the options instance is invalid</returns>
    /// <seealso cref="ThrowIfInvalid"/>
    public bool TryGetValidationErrors([NotNullWhen(true)] out List<string>? errors)
    {
        List<string>? list = null;

        if (this.Equals(default))
        {
            AddError("The CsvTokens<> instance is uninitialized.");
            errors = list!;
            return true;
        }

        if (Delimiter.Equals(StringDelimiter))
            AddError("Delimiter and StringDelimiter must not be equal.");

        if (NewLine.IsEmpty)
        {
            AddError("NewLine must not be empty.");
        }
        else
        {
            var newLine = NewLine.Span;

            if (newLine.Contains(Delimiter))
                AddError("NewLine must not contain Delimiter.");

            if (newLine.Contains(StringDelimiter))
                AddError("NewLine must not contain StringDelimiter.");
        }

        if (!Whitespace.IsEmpty)
        {
            var whitespace = Whitespace.Span;

            if (whitespace.Contains(Delimiter))
                AddError("Whitespace must not contain Delimiter.");

            if (whitespace.Contains(StringDelimiter))
                AddError("Whitespace must not contain StringDelimiter.");

            if (!NewLine.IsEmpty && whitespace.IndexOfAny(NewLine.Span) >= 0)
                AddError("NewLine and Whitespace must not have tokens in common.");
        }

        return (errors = list) is not null;

        void AddError(string message) => (list ??= new()).Add(message);
    }

    /// <summary>
    /// Throws if newline is empty, or there are duplicate symbols among any of the configuration tokens.
    /// </summary>
    /// <returns>The same instance</returns>
    /// <exception cref="CsvConfigurationException">The instance is invalid</exception>
    /// <seealso cref="TryGetValidationErrors"/>
    public CsvTokens<T> ThrowIfInvalid()
    {
        if (TryGetValidationErrors(out var errors)) Throw(errors);
        return this;

        [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
        static void Throw(List<string> errors)
        {
            throw new CsvConfigurationException(
                $"Invalid {typeof(CsvTokens<T>).ToTypeString()}: {string.Join(' ', errors)}");
        }
    }
}
