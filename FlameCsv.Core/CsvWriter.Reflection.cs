using System.Text;
using FlameCsv.Writing;

namespace FlameCsv;

static partial class CsvWriter
{
    /// <summary>
    /// Writes the CSV records to a string.
    /// </summary>
    /// <param name="values">Values to write to the string builder</param>
    /// <param name="options">Optional user configured options to use</param>
    /// <param name="builder">Optional builder to write the CSV to.</param>
    /// <returns>
    /// <see cref="StringBuilder"/> containing the CSV (same instance as <paramref name="builder"/> if provided)
    /// </returns>
    [RUF(Messages.CompiledExpressions), RDC(Messages.CompiledExpressions)]
    public static StringBuilder WriteToString<[DAM(Messages.ReflectionBound)] TValue>(
        IEnumerable<TValue> values,
        CsvOptions<char>? options = null,
        StringBuilder? builder = null)
    {
        ArgumentNullException.ThrowIfNull(values);

        options ??= CsvOptions<char>.Default;
        var dematerializer = options.TypeBinder.GetDematerializer<TValue>();

        builder ??= new();
        WriteCore(
            values,
            CsvFieldWriter.Create(new StringWriter(builder), options, bufferSize: DefaultBufferSize, leaveOpen: false),
            dematerializer);
        return builder;
    }
}
