using FlameCsv.Writing;
using System.Text;
using DAM = System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute;
using RUF = System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute;

namespace FlameCsv;

public static partial class CsvWriter
{
    /// <inheritdoc cref="Read{T,TValue}(CsvOptions{T},ReadOnlyMemory{T})"/>
    [RUF(Messages.CompiledExpressions)]
    public static Task<StringBuilder> WriteAsync<[DAM(Messages.ReflectionBound)] TValue>(
        IEnumerable<TValue> values,
        CsvOptions<char> options,
        CsvContextOverride<char> context = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(nameof(values));
        ArgumentNullException.ThrowIfNull(options);

        var stringBuilder = new StringBuilder(capacity: 1024);

        throw new NotImplementedException();
    }

    private static async Task<TValue> WriteValuesAsyncInternal<TValue>(
        IEnumerable<TValue> values,
        TextWriter textWriter,
        CsvReadingContext<char> context,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
        //CsvRecordWriter<char, CsvCharBufferWriter> csvWriter = WriteHelpers.Create(textWriter, options);
    }
}
